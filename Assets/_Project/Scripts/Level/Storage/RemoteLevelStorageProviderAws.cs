using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Editarrr.Level;
using Proyecto26;
using Singletons;
using UnityEditor;
using UnityEngine;

namespace Level.Storage
{
    public class RemoteLevelStorageProviderAws : IRemoteLevelStorageProvider
    {
        private const string AwsLevelUrlProd = "https://e1pvn0880k.execute-api.eu-north-1.amazonaws.com/dev";
        private const string AwsLevelUrlDev = "https://tlfb41owe5.execute-api.eu-north-1.amazonaws.com/dev";

        private const string AwsScreenshotUrlProd = "https://editarrr-screenshot-ethical-hare.s3.eu-north-1.amazonaws.com";
        private const string AwsScreenshotUrlDev = "https://editarrr-screenshot-ideal-wren.s3.eu-north-1.amazonaws.com";

        private const bool ShowDebug = false;

        public static string AwsLevelUrl => GetLevelUrl();
        public static string AwsScreenshotUrl => GetScreenshotUrl();

        public void Initialize()
        {
            // Nothing needed here.
        }

        private static string GetLevelUrl()
        {

#if DEPLOY_TARGET_PRODUCTION
            // Activate production env.
            return AwsLevelUrlProd;
#endif

            // Default to dev env.
            return AwsLevelUrlDev;
        }

        private static string GetScreenshotUrl()
        {

#if DEPLOY_TARGET_PRODUCTION
            // Activate production env.
            return AwsScreenshotUrlProd;
#endif

            // Default to dev env.
            return AwsScreenshotUrlDev;
        }

        public void Upload(LevelSave levelSave, RemoteLevelStorage_LevelUploadedCallback callback)
        {
            var userId = PreferencesManager.Instance.GetUserId();

            var tileData = new AwsTileData()
            {
                tiles = levelSave.Tiles
            };
            var request = new AwsLevel
            {
                id = levelSave.RemoteId,
                name = levelSave.Code,
                creator = new AwsCreator()
                {
                    name = levelSave.CreatorName,
                    id = userId
                },
                status = levelSave.Published ? "PUBLISHED" : "DRAFT",
                labels = levelSave.GetLabels(),
                version = levelSave.Version,
                data = new AwsLevelData()
                {
                    scaleX = levelSave.ScaleX,
                    scaleY = levelSave.ScaleY,
                    tiles = JsonUtility.ToJson(tileData),
                }
            };

            RestClient.Get<AwsLevel>($"{AwsLevelUrl}/levels/{levelSave.RemoteId}").Then(res =>
            {
                Debug.Log("UPLOAD - Existing level found for " + res.name);

                // Existing level found.
                this.Update(request, callback);
            }).Catch(_ =>
            {
                Debug.Log("UPLOAD - No level found for " + request.name);
                // No level found.
                this.Insert(request, callback);
            });
        }

        private void Insert(AwsLevel request, RemoteLevelStorage_LevelUploadedCallback callback)
        {
            RestClient.Post<AwsUploadResponse>($"{AwsLevelUrl}/levels", JsonUtility.ToJson(request)).Then(res =>
            {
                callback?.Invoke(request.name, res.id);
                this.LogMessage("Levels", JsonUtility.ToJson(res, true));

                UploadScreenshot(request.name);
            }).Catch(err => { this.LogMessage("Error", err.Message); });
        }

        private void Update(AwsLevel request, RemoteLevelStorage_LevelUploadedCallback callback)
        {
            RestClient.Patch<AwsUploadResponse>($"{AwsLevelUrl}/levels/{request.id}", JsonUtility.ToJson(request))
                .Then(res =>
                {
                    callback?.Invoke(request.name, request.id);
                    this.LogMessage("Levels", JsonUtility.ToJson(res, true));
                })
                .Finally(() =>
                {
                    UploadScreenshot(request.name);
                })
                .Catch(err =>
                {
                    this.LogMessage("Error", err.Message);
                    throw err;
                });
        }

        public void Download(string code, RemoteLevelStorage_LevelLoadedCallback callback)
        {
            RestClient.Get<AwsLevel>($"{AwsLevelUrl}/levels/{code}").Then(res =>
            {
                // @todo make sure we store the remote level id in the save.
                var save = new LevelSave(res.creator.id, res.creator.name, res.name);

                save.SetRemoteId(res.id);

                var tiles = JsonUtility.FromJson<AwsTileData>(res.data.tiles);
                var tileStates = new TileState[res.data.scaleX, res.data.scaleY];

                foreach (var tileSave in tiles.tiles)
                {
                    tileStates[tileSave.X, tileSave.Y] = new TileState(tileSave);
                }

                save.SetTiles(tileStates);
                save.SetTotalRatings(res.totalRatings);
                save.SetTotalScores(res.totalScores);
                save.SetVersion(res.version);

                foreach (var label in res.labels)
                {
                    save.SetLabel(label);
                }
                save.SetPublished(res.status == "PUBLISHED");
                callback?.Invoke(save);

                this.DoDownloadScreenshot(res.name);

                // @todo return level data.
                this.LogMessage(res.id, JsonUtility.ToJson(res, true));
            }).Catch(err =>
            {
                callback?.Invoke(null);
                this.LogMessage("Error", err.Message);
            });
        }

        public void DownloadScreenshot(string code, RemoteLevelStorage_LevelScreenshotDownloadedCallback callback)
        {
            this.DoDownloadScreenshot(code, callback);
        }

        private async void UploadScreenshot(string code)
        {
#if UNITY_WEBGL
            // Webgl needs a custom solution for uploading image files.
            return;
#endif
            var uploadUrl = $"{AwsLevelUrl}/screenshot/{code}.png";
            var imagePath = LocalLevelStorageManager.LocalRootDirectory + code + "/screenshot.png";
            using (var httpClient = new HttpClient())
            {
                // Load the image data from the file path
                var imageBytes = await File.ReadAllBytesAsync(imagePath);

                // Create a ByteArrayContent from the image data
                var imageContent = new ByteArrayContent(imageBytes);

                // Send the POST request
                var response = await httpClient.PostAsync(uploadUrl, imageContent);
                if (response.IsSuccessStatusCode)
                {
                    Debug.Log("Image uploaded successfully");
                }
                else
                {
                    Debug.LogError("Error uploading image. Status code: " + response.StatusCode);
                }
            }
        }

        private async void DoDownloadScreenshot(string code, RemoteLevelStorage_LevelScreenshotDownloadedCallback callback = null)
        {
            // @todo Move the storage to the LocalLevelStorageManager.
            var url = $"{AwsScreenshotUrl}/{code}.png";
            using (var httpClient = new HttpClient())
            {
                // Send an HTTP GET request to download the image
                var response = await httpClient.GetAsync(url);
                var saveDirectory = LocalLevelStorageManager.LocalRootDirectory + code;

                if (response.IsSuccessStatusCode)
                {
                    // Ensure the save directory exists
                    Directory.CreateDirectory(saveDirectory);

                    // Get the filename from the URL
                    var fileName = "screenshot.png";

                    // Combine the save directory and filename to get the full path
                    var fullPath = Path.Combine(saveDirectory, fileName);

                    // Read the image content and save it to the specified directory
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(fullPath, imageBytes);

                    Debug.Log("Image downloaded and saved to: " + fullPath);
                    callback?.Invoke();
                }
                else
                {
                    Debug.LogError($"Error downloading image for level {code}. Status code: " + response.StatusCode);
                    Debug.LogError(url);
                }
            }
        }

        public void LoadAllLevelData(RemoteLevelStorage_AllLevelsLoadedCallback callback, RemoteLevelLoadQuery? query = null)
        {
            string limit = "10";
            string cursor = "";
            if (query != null)
            {
                limit = query.Value.limit.ToString();
                cursor = query.Value.cursor;
            }

            string queryParams = $"?limit={limit}";
            queryParams += cursor.Length > 0 ? $"&cursor={cursor}" : "";

            // Get request to /levels
            RestClient.Get<AwsLevels>($"{AwsLevelUrl}/levels{queryParams}").Then(res =>
            {
                var levelStubs = new List<LevelStub>();
                foreach (var level in res.levels)
                {
                    var levelStub = new LevelStub(level.name, level.creator.id, level.creator.name, level.id,
                        level.status == "PUBLISHED");

                    levelStub.SetTotalRatings(level.totalRatings);
                    levelStub.SetTotalScores(level.totalScores);

                    foreach (var label in level.labels)
                    {
                        levelStub.SetLabel(label);
                    }

                    levelStubs.Add(levelStub);
                }

                callback?.Invoke(levelStubs.ToArray(), res.cursor);
                this.LogMessage("Levels", JsonUtility.ToJson(res, true));
            }).Catch(err =>
            {
                callback?.Invoke(null);
                this.LogMessage("Error", err.Message);
            });
        }

        public bool SupportsLeaderboards()
        {
            return true;
        }

        public void SubmitScore(float score, LevelSave levelSave, RemoteScoreStorage_ScoreSubmittedCallback callback)
        {
            string userId = PreferencesManager.Instance.GetUserId();
            string userName = PreferencesManager.Instance.GetUserName();

            var request = new AwsScoreRequest
            {
                code = levelSave.Code,
                score = score.ToString(),
                creator = userId,
                creatorName = userName
            };

            RestClient.Post<AwsUploadResponse>($"{AwsLevelUrl}/levels/{levelSave.RemoteId}/scores", JsonUtility.ToJson(request)).Then(res =>
            {
                Debug.Log("Score uploaded for level: " + levelSave.Code);
                callback?.Invoke(levelSave.Code, res.id);
            }).Catch(err => { this.LogMessage("Error", err.Message); });
        }

        public void GetScoresForLevel(string code, RemoteScoreStorage_AllScoresLoadedCallback callback)
        {
            string url = $"{AwsLevelUrl}/levels/{code}/scores?de-dupe-by-user=true";
            // Get request to /levels/{id}/scores
            RestClient.Get<AwsScores>(url).Then(res =>
            {
                var scoreStubs = new List<ScoreStub>();
                foreach (var score in res.scores)
                {
                    float scoreValue = float.Parse(score.score);
                    var levelStub = new ScoreStub(score.code, score.creator.id, score.creator.name, scoreValue);
                    scoreStubs.Add(levelStub);
                }

                callback?.Invoke(scoreStubs.ToArray());
                this.LogMessage("Scores URL", JsonUtility.ToJson(res, true));
                this.LogMessage("Scores", url);
            }).Catch(err =>
            {
                callback?.Invoke(null);
                this.LogMessage("Error", err.Message);
            });
        }

        public void SubmitRating(int rating, LevelSave levelSave, RemoteRatingStorage_RatingSubmittedCallback callback)
        {
            string userId = PreferencesManager.Instance.GetUserId();
            string userName = PreferencesManager.Instance.GetUserName();

            var request = new AwsRatingRequest
            {
                code = levelSave.Code,
                rating = rating,
                creator = userId,
                creatorName = userName
            };

            RestClient.Post<AwsUploadResponse>($"{AwsLevelUrl}/levels/{levelSave.RemoteId}/ratings", JsonUtility.ToJson(request)).Then(res =>
            {
                Debug.Log("Rating uploaded for level: " + levelSave.Code);
                callback?.Invoke(levelSave.Code, res.id);
            }).Catch(err => { this.LogMessage("Error", err.Message); });
        }

        public void GetRatingsForLevel(string code, RemoteRatingStorage_AllRatingsLoadedCallback callback)
        {
            // Get request to /levels/{id}/ratings
            RestClient.Get<AwsRatings>($"{AwsLevelUrl}/levels/{code}/ratings").Then(res =>
            {
                var ratingStubs = new List<RatingStub>();
                foreach (var rating in res.ratings)
                {
                    var levelStub = new RatingStub(rating.code, rating.creator.id, rating.creator.name, rating.rating);
                    ratingStubs.Add(levelStub);
                }

                callback?.Invoke(ratingStubs.ToArray());
                this.LogMessage("Ratings", JsonUtility.ToJson(res, true));
            }).Catch(err =>
            {
                callback?.Invoke(null);
                this.LogMessage("Error", err.Message);
            });
        }


        private void LogMessage(string title, string message)
        {
#if UNITY_EDITOR
            if (ShowDebug)
            {
                EditorUtility.DisplayDialog(title, message, "Ok");
            }
            else
            {
                Debug.Log(message);
            }
#else
		    Debug.Log(message);
#endif
        }
    }

    #region AWS Level Data Structures

    [Serializable]
    public class AwsLevels
    {
        public AwsLevel[] levels;
        public string cursor;
    }

    [Serializable]
    public class AwsLevel
    {
        public string id;
        public string name;
        public AwsCreator creator;
        public string status;
        public uint createdAt;
        public uint updatedAt;
        public string[] labels;
        public int totalRatings;
        public int totalScores;
        public int version;
        public AwsLevelData data;
    }

    [Serializable]
    public class AwsCreator
    {
        public string id;
        public string name;
    }

    [Serializable]
    public class AwsLevelData
    {
        public int scaleX;
        public int scaleY;
        public string tiles;
    }

    [Serializable]
    public class AwsTileData
    {
        public TileSave[] tiles;
    }

    [Serializable]
    public class AwsUploadResponse
    {
        public string message;

        // @todo update the web interface.
        public string id;
        public string data;
    }

    #endregion

    #region AWS Score Data Structures

    [Serializable]
    public class AwsScores
    {
        public AwsScore[] scores;
    }

    public class AwsScoreRequest
    {
        public string code;
        public string score;
        public string creator;
        public string creatorName;
    }

    [Serializable]
    public class AwsScore
    {
        public string scoreId;
        public string levelId;
        public string score;
        public string code;
        public AwsCreator creator;
        public uint submittedAt;
    }

    #endregion


    #region AWS Rating Data Structures

    [Serializable]
    public class AwsRatings
    {
        public AwsRating[] ratings;
    }

    public class AwsRatingRequest
    {
        public string code;
        public int rating;
        public string creator;
        public string creatorName;
    }

    [Serializable]
    public class AwsRating
    {
        public string scoreId;
        public string levelId;
        public int rating;
        public string code;
        public AwsCreator creator;
        public uint submittedAt;
    }

    #endregion
}
