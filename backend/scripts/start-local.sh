#!/bin/bash
set -eo pipefail

# Steps
# 1. Set up mock AWS creds + config
# 2. Start the local DynamoDB
# 3. Set up the table for the DynamoDB
# 4. Start the Nodejs Lambda + local API Gateway for querying

PORT=8000
DDB="dynamodb"
TABLE_NAME="editarrr-level-storage"

# Set up mock AWS credentials
if [ ! -d ~/.aws ]; then 
    echo "No AWS credentials found. Setting up fakse AWS config & creds so that AWS commands can be run..."
    mkdir -p ~/.aws
    echo -e "[default]\nregion = eu-north-1" > ~/.aws/config
    echo -e "[default]\naws_access_key_id=fakeAccessKey\naws_secret_access_key=fakeSecretKey" > ~/.aws/credentials; 
fi


# Start the local DynamoDB Docker container
if docker ps -f "name=${DDB}" --format '{{.Names}}' | grep -q "^${DDB}$"; then
    echo "Container '${DDB}' is already running."
else
    if docker ps -a -f "name=${DDB}" --format '{{.Status}}' | grep -q "^Exited"; then
        echo "Container '${DDB}' exists but is exited. Restarting..."
        docker start ${DDB}
    else
        docker run \
            --name dynamodb \
            -p ${PORT}:${PORT} \
            -d amazon/dynamodb-local \
            -jar DynamoDBLocal.jar
    fi
    sleep 5 # Give DynamoDB 5 seconds to start up
fi
# Other helpful Docker commands:
# docker stop ${DDB}
# docker rm ${DDB}


# Set up the DynamoDB Table
if aws dynamodb list-tables --endpoint-url http://localhost:${PORT} --query "TableNames[?(@=='${TABLE_NAME}')]" --output text | grep -q "^${TABLE_NAME}"; then
    echo "Table '${TABLE_NAME}' already exists."
else
    aws dynamodb create-table \
        --table-name ${TABLE_NAME} \
        --attribute-definitions \
            AttributeName=pk,AttributeType=S \
            AttributeName=sk,AttributeType=S \
            AttributeName=levelCreatorId,AttributeType=S \
            AttributeName=levelStatus,AttributeType=S \
            AttributeName=levelUpdatedAt,AttributeType=N \
        --key-schema \
            AttributeName=pk,KeyType=HASH \
            AttributeName=sk,KeyType=RANGE \
        --global-secondary-indexes \
            "[
                {
                    \"IndexName\": \"levelCreatorId-levelUpdatedAt-index\",
                    \"KeySchema\": [{\"AttributeName\":\"levelCreatorId\",\"KeyType\":\"HASH\"},
                                    {\"AttributeName\":\"levelUpdatedAt\",\"KeyType\":\"RANGE\"}],
                    \"Projection\":{
                        \"ProjectionType\":\"INCLUDE\",
                        \"NonKeyAttributes\":[\"pk\",\"levelName\",\"levelStatus\"]
                    },
                    \"ProvisionedThroughput\": {
                        \"ReadCapacityUnits\": 1,
                        \"WriteCapacityUnits\": 1
                    }
                },
                {
                    \"IndexName\": \"levelStatus-levelUpdatedAt-index\",
                    \"KeySchema\": [{\"AttributeName\":\"levelStatus\",\"KeyType\":\"HASH\"},
                                    {\"AttributeName\":\"levelUpdatedAt\",\"KeyType\":\"RANGE\"}],
                    \"Projection\":{
                        \"ProjectionType\":\"INCLUDE\",
                        \"NonKeyAttributes\":[\"pk\",\"levelName\",\"levelCreatorId\"]
                    },
                    \"ProvisionedThroughput\": {
                        \"ReadCapacityUnits\": 1,
                        \"WriteCapacityUnits\": 1
                    }
                }
            ]" \
        --provisioned-throughput ReadCapacityUnits=1,WriteCapacityUnits=1 \
        --endpoint-url http://localhost:${PORT}
fi
# Other helpful DynamoDB commands:
# aws dynamodb list-tables --endpoint-url http://localhost:${PORT}
# aws dynamodb delete-table --endpoint-url http://localhost:${PORT} --table-name <table-name> 


# Start the Nodejs Lambda + API Gateway for local queries
DDB_IP_ADDR=$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' ${DDB})
if [[ $my_variable == *"Error"* ]]; then
  echo "Failed to determine the IP Address of the DynamoDB Container: '${DDB_IP_ADDR}'"
  exit 1
fi
echo "IP Address of DynamoDB Container: ${DDB_IP_ADDR}"
echo "Starting Lambda..."
(cd ./lambda && sam local start-api --parameter-overrides ParameterKey=DdbIpAddr,ParameterValue=${DDB_IP_ADDR})