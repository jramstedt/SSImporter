using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using SystemShock.Object;
using SystemShock.Resource;

namespace SSImporter.Resource {
    public class ObjectPropertyImport {
        [MenuItem("Assets/System Shock/5. Import Object Properties", false, 1005)]
        public static void Init() {
            CreateObjectPropertyAssets();
        }

        [MenuItem("Assets/System Shock/5. Import Object Properties", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        /*
         * Similar table is in System Shock executable
         */
        public static ObjectDeclaration[][] ObjectDeclarations = new ObjectDeclaration[][] { 
                new ObjectDeclaration[] { // 00 Weapons
                    new ObjectDeclaration<Weapon, Weapon.SemiAutomatic>(5),
                    new ObjectDeclaration<Weapon, Weapon.FullAutomatic>(2),
                    new ObjectDeclaration<Weapon, Weapon.Projectile>(2),
                    new ObjectDeclaration<Weapon, Weapon.Melee>(2),
                    new ObjectDeclaration<Weapon, Weapon.Beam>(3),
                    new ObjectDeclaration<Weapon, Weapon.EnergyProjectile>(2)
                },
                new ObjectDeclaration[] { // 01 Ammunition
                    new ObjectDeclaration<Ammunition, Ammunition.All>(2),
                    new ObjectDeclaration<Ammunition, Ammunition.All>(2),
                    new ObjectDeclaration<Ammunition, Ammunition.All>(3),
                    new ObjectDeclaration<Ammunition, Ammunition.All>(2),
                    new ObjectDeclaration<Ammunition, Ammunition.All>(2),
                    new ObjectDeclaration<Ammunition, Ammunition.All>(2),
                    new ObjectDeclaration<Ammunition, Ammunition.All>(2)
                },
                new ObjectDeclaration[] { // 02 Projectiles
                    new ObjectDeclaration<Projectile, Projectile.Tracer>(6),
                    new ObjectDeclaration<Projectile, Projectile.Normal>(16),
                    new ObjectDeclaration<Projectile, Projectile.Seeker>(2)
                },
                new ObjectDeclaration[] { // 03 Grenades & Explosives
                    new ObjectDeclaration<Explosive, Explosive.Grenade>(5),
                    new ObjectDeclaration<Explosive, Explosive.Bomb>(3)
                },
                new ObjectDeclaration[] { // 04 Patches
                    new ObjectDeclaration<DermalPatch, DermalPatch.All>(7)
                },
                new ObjectDeclaration[] { // 05 Hardware
                    new ObjectDeclaration<Hardware, Hardware.All>(5),
                    new ObjectDeclaration<Hardware, Hardware.All>(10)
                },
                new ObjectDeclaration[] { // 06 Software & Logs
                    new ObjectDeclaration<SoftwareAndLog, SoftwareAndLog.All>(7),
                    new ObjectDeclaration<SoftwareAndLog, SoftwareAndLog.All>(3),
                    new ObjectDeclaration<SoftwareAndLog, SoftwareAndLog.All>(4),
                    new ObjectDeclaration<SoftwareAndLog, SoftwareAndLog.All>(5),
                    new ObjectDeclaration<SoftwareAndLog, SoftwareAndLog.All>(3),
                },
                new ObjectDeclaration[] { // 07 Decorations
                    new ObjectDeclaration<Decoration, Decoration.All>(9),
                    new ObjectDeclaration<Decoration, Decoration.All>(10),
                    new ObjectDeclaration<Decoration, Decoration.All>(11),
                    new ObjectDeclaration<Decoration, Decoration.All>(4),
                    new ObjectDeclaration<Decoration, Decoration.All>(9),
                    new ObjectDeclaration<Decoration, Decoration.All>(8),
                    new ObjectDeclaration<Decoration, Decoration.All>(16),
                    new ObjectDeclaration<Decoration, Decoration.All>(10)
                },
                new ObjectDeclaration[] { // 08 Items
                    new ObjectDeclaration<Item, Item.Common>(8),
                    new ObjectDeclaration<Item, Item.Common>(10),
                    new ObjectDeclaration<Item, Item.Common>(15),
                    new ObjectDeclaration<Item, Item.Common>(6),
                    new ObjectDeclaration<Item, Item.Common>(12),
                    new ObjectDeclaration<Item, Item.Cyberspace>(12),
                    new ObjectDeclaration<Item, Item.Common>(9),
                    new ObjectDeclaration<Item, Item.Quest>(8)
                },
                new ObjectDeclaration[] { // 09 Interfaces (Switches & Panels)
                    new ObjectDeclaration<Interface, Interface.Common>(9),
                    new ObjectDeclaration<Interface, Interface.Common>(7),
                    new ObjectDeclaration<Interface, Interface.Common>(3),
                    new ObjectDeclaration<Interface, Interface.Common>(11),
                    new ObjectDeclaration<Interface, Interface.Vending>(2),
                    new ObjectDeclaration<Interface, Interface.Common>(3)
                },
                new ObjectDeclaration[] { // 0A Doors & Gratings
                    new ObjectDeclaration<DoorAndGrating, DoorAndGrating.All>(10),
                    new ObjectDeclaration<DoorAndGrating, DoorAndGrating.All>(9),
                    new ObjectDeclaration<DoorAndGrating, DoorAndGrating.All>(7),
                    new ObjectDeclaration<DoorAndGrating, DoorAndGrating.All>(5),
                    new ObjectDeclaration<DoorAndGrating, DoorAndGrating.All>(10)
                },
                new ObjectDeclaration[] { // 0B Animated 
                    new ObjectDeclaration<Animated, Animated.All>(9),
                    new ObjectDeclaration<Animated, Animated.All>(11),
                    new ObjectDeclaration<Animated, Animated.All>(14)
                },
                new ObjectDeclaration[] { // 0C Traps & Triggers
                    new ObjectDeclaration<Trigger, Trigger.All>(13),
                    new ObjectDeclaration<Trigger, Trigger.All>(1),
                    new ObjectDeclaration<Trigger, Trigger.All>(5),
                },
                new ObjectDeclaration[] { // 0D Containers
                    new ObjectDeclaration<Container, Container.All>(3),
                    new ObjectDeclaration<Container, Container.All>(3),
                    new ObjectDeclaration<Container, Container.All>(4),
                    new ObjectDeclaration<Container, Container.All>(8),
                    new ObjectDeclaration<Container, Container.All>(13),
                    new ObjectDeclaration<Container, Container.All>(7),
                    new ObjectDeclaration<Container, Container.All>(8)
                },
                new ObjectDeclaration[] { // 0E Enemies
                    new ObjectDeclaration<Enemy, Enemy.Mutant>(9),
                    new ObjectDeclaration<Enemy, Enemy.Bot>(12),
                    new ObjectDeclaration<Enemy, Enemy.Cyborg>(7),
                    new ObjectDeclaration<Enemy, Enemy.Cyberspace>(7),
                    new ObjectDeclaration<Enemy, Enemy.Boss>(2)
                }
            };

        private static void CreateObjectPropertyAssets() {
            string filePath = PlayerPrefs.GetString(@"SSHOCKRES");

            string objpropPath = filePath + @"\DATA\objprop.dat";

            if (!File.Exists(objpropPath))
                return;

            try {
                AssetDatabase.StartAssetEditing();

                if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                    AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

                StringLibrary stringLibrary = StringLibrary.GetLibrary(@"cybstrng.res");
                CyberString objectNames = stringLibrary.GetStrings(KnownChunkId.ObjectNames);
                CyberString objectShortNames = stringLibrary.GetStrings(KnownChunkId.ShortObjectNames);

                ObjectPropertyLibrary objectPropertyLibrary = ScriptableObject.CreateInstance<ObjectPropertyLibrary>();
                AssetDatabase.CreateAsset(objectPropertyLibrary, @"Assets/SystemShock/objprop.dat.asset");

                using (FileStream fileStream = new FileStream(objpropPath, FileMode.Open, FileAccess.Read)) {
                    BinaryReader binaryReader = new BinaryReader(fileStream, Encoding.ASCII);

                    uint header = binaryReader.ReadUInt32();

                    if (header != 0x0000002D)
                        throw new ArgumentException(string.Format(@"File type is not supported ({0})", header));

                    uint nameIndex = 0;

                    for (uint classIndex = 0; classIndex < ObjectDeclarations.Length; ++classIndex) {
                        ObjectDeclaration[] objectDataSubclass = ObjectDeclarations[classIndex];

                        for (uint subclassIndex = 0; subclassIndex < objectDataSubclass.Length; ++subclassIndex) {
                            ObjectDeclaration objectDataType = objectDataSubclass[subclassIndex];

                            uint idBase = classIndex << 16 | subclassIndex << 8;

                            string typeName = @"SystemShock.DataObjects." + objectDataType.GetGenericType().Name + objectDataType.GetSpecificType().Name + @", Assembly-CSharp";

                            for (uint typeIndex = 0; typeIndex < objectDataType.Count; ++typeIndex) {
                                string fullName = objectNames[nameIndex];
                                string shortName = objectShortNames[nameIndex];

                                Type unityType = Type.GetType(typeName);
                                if (unityType == null)
                                    throw new Exception(@"Type " + typeName + @" not found.");

                                ObjectData objectData = ScriptableObject.CreateInstance(unityType) as ObjectData;
                                objectData.name = fullName.ToLowerInvariant();
                                objectData.FullName = fullName;
                                objectData.ShortName = shortName;
                                objectData.hideFlags = HideFlags.HideInHierarchy;

                                objectPropertyLibrary.AddObject(idBase | typeIndex, objectData);

                                ++nameIndex;
                            }

                            #region Generic data
                            for (uint typeIndex = 0; typeIndex < objectDataType.Count; ++typeIndex) {
                                ObjectData objectData = objectPropertyLibrary.GetObject<ObjectData>(idBase | typeIndex);
                                objectData.SetGeneric(binaryReader.Read(objectDataType.GetGenericType()));
                            }
                            #endregion
                        }

                        for (uint subclassIndex = 0; subclassIndex < objectDataSubclass.Length; ++subclassIndex) {
                            ObjectDeclaration objectDataType = objectDataSubclass[subclassIndex];

                            uint idBase = classIndex << 16 | subclassIndex << 8;

                            #region Specific data
                            for (uint typeIndex = 0; typeIndex < objectDataType.Count; ++typeIndex) {
                                ObjectData objectData = objectPropertyLibrary.GetObject<ObjectData>(idBase | typeIndex);
                                objectData.SetSpecific(binaryReader.Read(objectDataType.GetSpecificType()));
                            }
                            #endregion
                        }
                    }

                    binaryReader.ReadByte(); // Padding?

                    #region Base data
                    for (uint classIndex = 0; classIndex < ObjectDeclarations.Length; ++classIndex) {
                        ObjectDeclaration[] objectDataSubclass = ObjectDeclarations[classIndex];

                        for (uint subclassIndex = 0; subclassIndex < objectDataSubclass.Length; ++subclassIndex) {
                            ObjectDeclaration objectDataType = objectDataSubclass[subclassIndex];

                            uint idBase = classIndex << 16 | subclassIndex << 8;

                            for (uint typeIndex = 0; typeIndex < objectDataType.Count; ++typeIndex) {
                                ObjectData objectData = objectPropertyLibrary.GetObject<ObjectData>(idBase | typeIndex);
                                //objectData.SetBase(binaryReader.Read(objectDataType.GetBaseType()));
                                objectData.Base = binaryReader.Read<BaseProperties>();

                                //if ((objectData.Base.Flags & (ushort)Flags.F12) != 0)
                                //    Debug.Log(classIndex + ":" + subclassIndex + ":" + typeIndex + " " + objectData.FullName + " " + Convert.ToString(objectData.Base.Flags, 2).PadLeft(16, '0'));

                                AssetDatabase.AddObjectToAsset(objectData, objectPropertyLibrary);

                                EditorUtility.SetDirty(objectData);
                            }
                        }
                    }
                    #endregion

                    EditorUtility.SetDirty(objectPropertyLibrary);
                }

                ObjectFactory.GetController().AddLibrary(objectPropertyLibrary);
            } finally {
                AssetDatabase.StopAssetEditing();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();
        }
    }

    public abstract class ObjectDeclaration {
        public uint Count;

        //public abstract Type GetBaseType();
        public abstract Type GetGenericType();
        public abstract Type GetSpecificType();
    }

    public sealed class ObjectDeclaration<G, S> : ObjectDeclaration
        where G : struct
        where S : struct {

        public ObjectDeclaration(uint count) {
            Count = count;
        }

        //public override Type GetBaseType() { return typeof(BaseProperties); }
        public override Type GetGenericType() { return typeof(G); }
        public override Type GetSpecificType() { return typeof(S); }
    }

}