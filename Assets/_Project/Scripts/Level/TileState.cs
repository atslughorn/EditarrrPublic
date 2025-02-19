﻿using Editarrr.LevelEditor;
using Editarrr.Misc;
using System;
using UnityEngine;

namespace Editarrr.Level
{
    public class TileState
    {
        public TileType Foreground { get; private set; }
        public Rotation ForegroundRotation { get; private set; }

        public TileType Background { get; private set; }
        public Rotation BackgroundRotation { get; private set; }

        public TileConfig Config { get; set; }

        public TileState(TileType foreground, TileType background, Rotation foregroundRotation, Rotation backgroundRotation, TileConfig config)
        {
            this.Foreground = foreground;
            this.Background = background;
            this.ForegroundRotation = foregroundRotation;
            this.BackgroundRotation = backgroundRotation;
            this.Config = config;

            if (this.Foreground == TileType.MovingPlatform)
                UnityEngine.Debug.Log(this.Config);
        }

        public TileState(TileSave tileSave)
        {
            this.Foreground = tileSave.Foreground;
            this.Background = tileSave.Background;
            this.ForegroundRotation = tileSave.ForegroundRotation;
            this.BackgroundRotation = tileSave.BackgroundRotation;

            this.Config = this.ReadJSONData(tileSave.Config);
        }

        public TileSave CreateSave(int x, int y)
        {
            return new TileSave(x, y, this);
        }

        private TileConfig ReadJSONData(int[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            switch (this.Foreground)
            {
                case TileType.MovingPlatform:
                    return new MovingPlatformConfig(data);
                case TileType.Lever:
                    return new LeverConfig(data);
                case TileType.LeverBlock:
                    return new LeverBlockConfig(data);
                case TileType.CrackedPlank:
                    return new CrackedPlankConfig(data);
                default:
                    {
                        Debug.LogError("Config-Values found but there is no Config Class specified!");
                        return null;
                    }
            }
        }
    }
}
