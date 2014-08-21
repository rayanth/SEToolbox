﻿namespace SEToolbox.Interop
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Xml;

    using Microsoft.Xml.Serialization.GeneratedAssembly;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Common.ObjectBuilders.Definitions;
    using SEToolbox.Support;
    using VRageMath;

    /// <summary>
    /// Helper api for accessing and interacting with Space Engineers content.
    /// </summary>
    public static class SpaceEngineersApi
    {
        #region Serializers

        public static T ReadSpaceEngineersFile<T, TS>(Stream stream)
        where TS : XmlSerializer1
        {
            var settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
            };

            object obj;

            using (var xmlReader = XmlReader.Create(stream, settings))
            {
                var serializer = (TS)Activator.CreateInstance(typeof(TS));
                //serializer.UnknownAttribute += serializer_UnknownAttribute;
                //serializer.UnknownElement += serializer_UnknownElement;
                //serializer.UnknownNode += serializer_UnknownNode;
                obj = serializer.Deserialize(xmlReader);
            }

            return (T)obj;
        }

        [Obsolete]
        public static bool TryReadSpaceEngineersFile<T, TS>(string filename, out T entity)
             where TS : XmlSerializer1
        {
            try
            {
                entity = ReadSpaceEngineersFile<T, TS>(filename);
                return true;
            }
            catch
            {
                entity = default(T);
                return false;
            }
        }

        [Obsolete]
        public static T ReadSpaceEngineersFile<T, TS>(string filename)
            where TS : XmlSerializer1
        {
            var settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                // Space Engineers is able to read partially corrupted files,
                // which means Keen probably aren't using any XML reader settings in general. 
            };

            object obj = null;

            if (File.Exists(filename))
            {
                using (var xmlReader = XmlReader.Create(filename, settings))
                {
                    var serializer = (TS)Activator.CreateInstance(typeof(TS));
                    obj = serializer.Deserialize(xmlReader);
                }
            }

            return (T)obj;
        }

        public static bool TryReadSpaceEngineersFile<T>(string filename, out T entity, out bool isCompressed)
        {
            try
            {
                entity = ReadSpaceEngineersFile<T>(filename, out isCompressed);
                return true;
            }
            catch
            {
                entity = default(T);
                isCompressed = false;
                return false;
            }
        }

        public static T ReadSpaceEngineersFile<T>(string filename)
        {
            bool isCompressed;
            return ReadSpaceEngineersFile<T>(filename, out isCompressed);
        }

        public static T ReadSpaceEngineersFile<T>(string filename, out bool isCompressed)
        {
            isCompressed = false;

            if (File.Exists(filename))
            {
                using (var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    var b1 = fileStream.ReadByte();
                    var b2 = fileStream.ReadByte();
                    isCompressed = (b1 == 0x1f && b2 == 0x8b);
                    fileStream.Position = 0;

                    if (isCompressed)
                    {
                        using (var outStream = new MemoryStream())
                        {
                            using (var zip = new GZipStream(fileStream, CompressionMode.Decompress))
                            {
                                zip.CopyTo(outStream);
                                Debug.WriteLine("Decompressed from {0:#,###0} bytes to {1:#,###0} bytes.", fileStream.Length, outStream.Length);
                            }
                            outStream.Position = 0;
                            return ReadSpaceEngineersFileRaw<T>(outStream);
                        }
                    }
                    
                    return ReadSpaceEngineersFileRaw<T>(fileStream);
                }
            }

            return default(T);
        }

        public static T ReadSpaceEngineersFile<T>(Stream fileStream, out bool isCompressed)
        {
            isCompressed = false;

            if (fileStream != null)
            {
                var b1 = fileStream.ReadByte();
                var b2 = fileStream.ReadByte();
                isCompressed = (b1 == 0x1f && b2 == 0x8b);
                fileStream.Position = 0;

                if (isCompressed)
                {
                    using (var outStream = new MemoryStream())
                    {
                        using (var zip = new GZipStream(fileStream, CompressionMode.Decompress))
                        {
                            zip.CopyTo(outStream);
                            Debug.WriteLine("Decompressed from {0:#,###0} bytes to {1:#,###0} bytes.", fileStream.Length, outStream.Length);
                        }
                        outStream.Position = 0;
                        return ReadSpaceEngineersFileRaw<T>(outStream);
                    }
                }
                
                return ReadSpaceEngineersFileRaw<T>(fileStream);
            }

            return default(T);
        }

        public static T ReadSpaceEngineersFileRaw<T>(Stream stream)
        {
            var contract = new XmlSerializerContract();
            object obj = default(T);

            // Space Engineers is able to read partially corrupted xml files,
            // which means Keen probably aren't using any XML reader settings in general. 
            using (var xmlReader = XmlReader.Create(stream))
            {
                var serializer = contract.GetSerializer(typeof(T));
                if (serializer != null)
                    obj = serializer.Deserialize(xmlReader);
            }

            return (T)obj;
        }

        public static T Deserialize<T>(string xml)
        {
            using (var textReader = new StringReader(xml))
            {
                return (T)(new XmlSerializerContract().GetSerializer(typeof(T)).Deserialize(textReader));
            }
        }

        public static string Serialize<T>(object item)
        {
            using (var textWriter = new StringWriter())
            {
                new XmlSerializerContract().GetSerializer(typeof(T)).Serialize(textWriter, item);
                return textWriter.ToString();
            }
        }

        public static bool WriteSpaceEngineersFile<T, TS>(T sector, string filename)
            where TS : XmlSerializer1
        {
            // How they appear to be writing the files currently.
            try
            {
                using (var xmlTextWriter = new XmlTextWriter(filename, null))
                {
                    xmlTextWriter.Formatting = Formatting.Indented;
                    xmlTextWriter.Indentation = 2;
                    var serializer = (TS)Activator.CreateInstance(typeof(TS));
                    serializer.Serialize(xmlTextWriter, sector);

                    xmlTextWriter.WriteComment(string.Format(" Saved '{0:o}' with SEToolbox version '{1}' ", DateTime.Now, GlobalSettings.GetVersion()));
                }
            }
            catch
            {
                return false;
            }

            //// How they should be doing it to support Unicode.
            //var settingsDestination = new XmlWriterSettings()
            //{
            //    Indent = true, // Set indent to false to compress.
            //    Encoding = new UTF8Encoding(false)   // codepage 65001 without signature. Removes the Byte Order Mark from the start of the file.
            //};

            //try
            //{
            //    using (var xmlWriter = XmlWriter.Create(filename, settingsDestination))
            //    {
            //        S serializer = (S)Activator.CreateInstance(typeof(S));
            //        serializer.Serialize(xmlWriter, sector);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    return false;
            //}

            return true;
        }

        #endregion

        #region GenerateEntityId

        public static long GenerateEntityId()
        {
            // Not the offical SE way of generating IDs, but its fast and we don't have to worry about a random seed.
            var buffer = Guid.NewGuid().ToByteArray();
            return BitConverter.ToInt64(buffer, 0);
        }

        #endregion

        #region FetchCubeBlockMass

        public static float FetchCubeBlockMass(MyObjectBuilderType typeId, MyCubeSize cubeSize, string subTypeid)
        {
            float mass = 0;

            var cubeBlockDefinition = GetCubeDefinition(typeId, cubeSize, subTypeid);

            if (cubeBlockDefinition != null)
            {
                foreach (var component in cubeBlockDefinition.Components)
                {
                    mass += SpaceEngineersCore.Definitions.Components.Where(c => c.Id.SubtypeId == component.Subtype).Sum(c => c.Mass) * component.Count;
                }
            }

            return mass;
        }

        public static void AccumulateCubeBlueprintRequirements(string subType, MyObjectBuilderType typeId, decimal amount, Dictionary<string, MyObjectBuilder_BlueprintDefinition.Item> requirements, out TimeSpan timeTaken)
        {
            var time = new TimeSpan();
            var bp = SpaceEngineersCore.Definitions.Blueprints.FirstOrDefault(b => b.Result.SubtypeId == subType && b.Result.Id.TypeId == typeId);
            if (bp != null)
            {
                foreach (var item in bp.Prerequisites)
                {
                    if (requirements.ContainsKey(item.SubtypeId))
                    {
                        // append existing
                        requirements[item.SubtypeId].Amount += (amount / bp.Result.Amount) * item.Amount;
                    }
                    else
                    {
                        // add new
                        requirements.Add(item.SubtypeId, new MyObjectBuilder_BlueprintDefinition.Item
                        {
                            Amount = (amount / bp.Result.Amount) * item.Amount,
                            TypeId = item.TypeId,
                            SubtypeId = item.SubtypeId,
                            Id = item.Id
                        });
                    }

                    var ticks = TimeSpan.TicksPerSecond * (decimal)bp.BaseProductionTimeInSeconds * amount;
                    var ts = new TimeSpan((long)ticks);
                    time += ts;
                }
            }

            timeTaken = time;
        }

        public static MyObjectBuilder_DefinitionBase GetDefinition(MyObjectBuilderType typeId, string subTypeId)
        {
            var cube = SpaceEngineersCore.Definitions.CubeBlocks.FirstOrDefault(d => d.Id.TypeId == typeId && d.Id.SubtypeId == subTypeId);
            if (cube != null)
            {
                return cube;
            }

            var item = SpaceEngineersCore.Definitions.PhysicalItems.FirstOrDefault(d => d.Id.TypeId == typeId && d.Id.SubtypeId == subTypeId);
            if (item != null)
            {
                return item;
            }

            var component = SpaceEngineersCore.Definitions.Components.FirstOrDefault(c => c.Id.TypeId == typeId && c.Id.SubtypeId == subTypeId);
            if (component != null)
            {
                return component;
            }

            var magazine = SpaceEngineersCore.Definitions.AmmoMagazines.FirstOrDefault(c => c.Id.TypeId == typeId && c.Id.SubtypeId == subTypeId);
            if (magazine != null)
            {
                return magazine;
            }

            return null;
        }

        public static float GetItemMass(MyObjectBuilderType typeId, string subTypeId)
        {
            var def = GetDefinition(typeId, subTypeId);
            if (def is MyObjectBuilder_PhysicalItemDefinition)
            {
                var item2 = def as MyObjectBuilder_PhysicalItemDefinition;
                return item2.Mass;
            }

            return 0;
        }

        public static float GetItemVolume(MyObjectBuilderType typeId, string subTypeId)
        {
            var def = GetDefinition(typeId, subTypeId);
            if (def is MyObjectBuilder_PhysicalItemDefinition)
            {
                var item2 = def as MyObjectBuilder_PhysicalItemDefinition;
                if (item2.Volume.HasValue)
                    return item2.Volume.Value;
            }

            return 0;
        }

        public static IList<MyObjectBuilder_VoxelMaterialDefinition> GetMaterialList()
        {
            return SpaceEngineersCore.Definitions.VoxelMaterials;
        }

        public static byte GetMaterialIndex(string materialName)
        {
            if (SpaceEngineersCore.MaterialIndex.ContainsKey(materialName))
                return SpaceEngineersCore.MaterialIndex[materialName];

            var material = SpaceEngineersCore.Definitions.VoxelMaterials.FirstOrDefault(m => m.Id.SubtypeId == materialName);
            var index = (byte)SpaceEngineersCore.Definitions.VoxelMaterials.ToList().IndexOf(material);
            SpaceEngineersCore.MaterialIndex.Add(materialName, index);
            return index;
        }

        public static string GetMaterialName(byte materialIndex, byte defaultMaterialIndex)
        {
            if (materialIndex <= SpaceEngineersCore.Definitions.VoxelMaterials.Length)
                return SpaceEngineersCore.Definitions.VoxelMaterials[materialIndex].Id.SubtypeId;

            return SpaceEngineersCore.Definitions.VoxelMaterials[defaultMaterialIndex].Id.SubtypeId;
        }

        public static string GetMaterialName(byte materialIndex)
        {
            return SpaceEngineersCore.Definitions.VoxelMaterials[materialIndex].Id.SubtypeId;
        }

        #endregion

        #region GetCubeDefinition

        public static MyObjectBuilder_CubeBlockDefinition GetCubeDefinition(MyObjectBuilderType typeId, MyCubeSize cubeSize, string subtypeId)
        {
            if (string.IsNullOrEmpty(subtypeId))
            {
                return SpaceEngineersCore.Definitions.CubeBlocks.FirstOrDefault(d => d.CubeSize == cubeSize && d.Id.TypeId == typeId);
            }

            return SpaceEngineersCore.Definitions.CubeBlocks.FirstOrDefault(d => d.Id.SubtypeId == subtypeId || (d.Variants != null && d.Variants.Any(v => subtypeId == d.Id.SubtypeId + v.Color)));
            // Returns null if it doesn't find the required SubtypeId.
        }

        #endregion

        #region GetBoundingBox

        public static BoundingBox GetBoundingBox(MyObjectBuilder_CubeGrid entity)
        {
            var min = new Vector3(int.MaxValue, int.MaxValue, int.MaxValue);
            var max = new Vector3(int.MinValue, int.MinValue, int.MinValue);

            foreach (var block in entity.CubeBlocks)
            {
                min.X = Math.Min(min.X, block.Min.X);
                min.Y = Math.Min(min.Y, block.Min.Y);
                min.Z = Math.Min(min.Z, block.Min.Z);
                max.X = Math.Max(max.X, block.Min.X);       // TODO: resolve cubetype size.
                max.Y = Math.Max(max.Y, block.Min.Y);
                max.Z = Math.Max(max.Z, block.Min.Z);
            }

            // scale box to GridSize
            var size = max - min;
            var len = entity.GridSizeEnum.ToLength();
            size = new Vector3(size.X * len, size.Y * len, size.Z * len);

            // translate box according to min/max, but reset origin.
            var bb = new BoundingBox(new Vector3(0, 0, 0), size);

            // TODO: translate for rotation.
            //bb. ????

            // translate position.
            bb.Translate(entity.PositionAndOrientation.Value.Position);


            return bb;
        }

        #endregion

        #region GetResourceName

        public static string GetResourceName(string value)
        {
            if (value == null)
                return null;

            Sandbox.Common.Localization.MyTextsWrapperEnum myText;

            if (Enum.TryParse<Sandbox.Common.Localization.MyTextsWrapperEnum>(value, out myText))
            {
                try
                {
                    return Sandbox.Common.Localization.MyTextsWrapper.GetFormatString(myText);
                }
                catch
                {
                    return value;
                }
            }

            return value;
        }
        
        #endregion
    }
}