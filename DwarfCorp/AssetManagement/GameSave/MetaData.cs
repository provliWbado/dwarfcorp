using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;

namespace DwarfCorp
{
    public class MetaData
    {
        public string OverworldFile { get; set; } // Todo: The overworld is known due to new system... KILLLLL!
        public Vector2 WorldOrigin { get; set; } // Todo: Kill?
        public float TimeOfDay { get; set; }
        public int Slice { get; set; }
        public WorldTime Time { get; set; }
        public Point3 NumChunks { get; set; }
        public String Version;
        public String Commit;
        
        public static string Extension = "meta";
        public static string CompressedExtension = "zmeta";

        public static MetaData CreateFromWorld(WorldManager World)
        {
            return new MetaData
            {
                OverworldFile = World.Settings.Overworld.Name,
                WorldOrigin = World.Settings.Origin,
                TimeOfDay = World.Sky.TimeOfDay,
                Time = World.Time,
                Slice = (int)World.Master.MaxViewingLevel,
                NumChunks = World.ChunkManager.WorldSize,
                Version = Program.Version,
                Commit = Program.Commit,
            };
        }
    }
}