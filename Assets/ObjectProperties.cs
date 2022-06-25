
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SS.ObjectProperties;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace SS.Resources {
  public class ObjectProperties : IDisposable {
    private const uint FILE_VERSION = 45;

    public readonly BlobAssetReference<ObjectDatas> ObjectDatasBlobAsset;

    public ObjectProperties(byte[] objPropData) {
      using var fileStream = new MemoryStream(objPropData, false);
      using var binaryReader = new BinaryReader(fileStream, Encoding.ASCII);

      uint version = binaryReader.ReadUInt32();
      if (version != FILE_VERSION)
        throw new NotSupportedException($"File version is not supported ({version})");

      using (var blobBuilder = new BlobBuilder(Allocator.Temp)) {
        ref var objectDataBlob = ref blobBuilder.ConstructRoot<ObjectDatas>();

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.WeaponProps, Weapon.NUM_GUN);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.PistolWeaponProps, Weapon.NUM_PISTOL_GUN);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.AutomaticWeaponProps, Weapon.NUM_AUTO_GUN);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ProjectileWeaponProps, Weapon.NUM_SPECIAL_GUN);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.MeleeWeaponProps, Weapon.NUM_HANDTOHAND_GUN);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.BeamWeaponProps, Weapon.NUM_BEAM_GUN);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.EnergyProjectileWeaponProps, Weapon.NUM_BEAMPROJ_GUN);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.AmmunitionProps, Ammunition.NUM_AMMO);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.PistolAmmunitionProps, Ammunition.NUM_PISTOL_AMMO);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.NeedleAmmunitionProps, Ammunition.NUM_NEEDLE_AMMO);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.MagnumAmmunitionProps, Ammunition.NUM_MAGNUM_AMMO);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.RifleAmmunitionProps, Ammunition.NUM_RIFLE_AMMO);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.FlechetteAmmunitionProps, Ammunition.NUM_FLECHETTE_AMMO);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.AutoAmmunitionProps, Ammunition.NUM_AUTO_AMMO);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ProjectileAmmunitionProps, Ammunition.NUM_PROJ_AMMO);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ProjectileProps, Projectile.NUM_PHYSICS);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.TracerProjectileProps, Projectile.NUM_TRACER_PHYSICS);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.SlowProjectileProps, Projectile.NUM_SLOW_PHYSICS);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.CameraProjectileProps, Projectile.NUM_CAMERA_PHYSICS);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ExplosiveProps, Explosive.NUM_GRENADE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.DirectExplosiveProps, Explosive.NUM_DIRECT_GRENADE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.TimedExplosiveProps, Explosive.NUM_TIMED_GRENADE);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.DrugProps, DermalPatch.NUM_DRUG);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.StatsDrugProps, DermalPatch.NUM_STATS_DRUG);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.HardwareProps, Hardware.NUM_HARDWARE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.GoggleHardwareProps, Hardware.NUM_GOGGLE_HARDWARE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.HardHardwareProps, Hardware.NUM_HARDWARE_HARDWARE);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.SoftwareProps, Software.NUM_SOFTWARE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.OffenseSoftwareProps, Software.NUM_OFFENSE_SOFTWARE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.DefenseSoftwareProps, Software.NUM_DEFENSE_SOFTWARE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.OneShotSoftwareProps, Software.NUM_ONESHOT_SOFTWARE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.MiscSoftwareProps, Software.NUM_MISC_SOFTWARE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.DataSoftwareProps, Software.NUM_DATA_SOFTWARE);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.DecorationProps, Decoration.NUM_BIGSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ElectronicDecorationProps, Decoration.NUM_ELECTRONIC_BIGSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.FurnitureDecorationProps, Decoration.NUM_FURNISHING_BIGSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.OnTheWallDecorationProps, Decoration.NUM_ONTHEWALL_BIGSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.LightDecorationProps, Decoration.NUM_LIGHT_BIGSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.LabGearDecorationProps, Decoration.NUM_LABGEAR_BIGSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.TechnoDecorationProps, Decoration.NUM_TECHNO_BIGSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.DecorDecorationProps, Decoration.NUM_DECOR_BIGSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.TerrainDecorationProps, Decoration.NUM_TERRAIN_BIGSTUFF);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ItemProps, Item.NUM_SMALLSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.UselessItemProps, Item.NUM_USELESS_SMALLSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.BrokenItemProps, Item.NUM_BROKEN_SMALLSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.CorpseItemProps, Item.NUM_CORPSELIKE_SMALLSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.GearItemProps, Item.NUM_GEAR_SMALLSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.CardsItemProps, Item.NUM_CARDS_SMALLSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.CyberspaceItemProps, Item.NUM_CYBER_SMALLSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.OnTheWallItemProps, Item.NUM_ONTHEWALL_SMALLSTUFF);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.PlotItemProps, Item.NUM_PLOT_SMALLSTUFF);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.FixtureProps, Fixture.NUM_FIXTURE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ControlFixtureProps, Fixture.NUM_CONTROL_FIXTURE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ReceptacleFixtureProps, Fixture.NUM_RECEPTACLE_FIXTURE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.TerminalFixtureProps, Fixture.NUM_TERMINAL_FIXTURE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.PanelFixtureProps, Fixture.NUM_PANEL_FIXTURE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.VendingFixtureProps, Fixture.NUM_VENDING_FIXTURE);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.CyberFixtureProps, Fixture.NUM_CYBER_FIXTURE);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.DoorsAndGratingProps, DoorAndGrating.NUM_DOOR);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.NormalDoorsAndGratingProps, DoorAndGrating.NUM_NORMAL_DOOR);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.DoorwayDoorsAndGratingProps, DoorAndGrating.NUM_DOORWAYS_DOOR);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ForceDoorsAndGratingProps, DoorAndGrating.NUM_FORCE_DOOR);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ElevatorDoorsAndGratingProps, DoorAndGrating.NUM_ELEVATOR_DOOR);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.SpecialDoorsAndGratingProps, DoorAndGrating.NUM_SPECIAL_DOOR);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.AnimatingProps, Animating.NUM_ANIMATING);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ObjectAnimatingProps, Animating.NUM_OBJECT_ANIMATING);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.TransitoryAnimatingProps, Animating.NUM_TRANSITORY_ANIMATING);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ExplosionAnimatingProps, Animating.NUM_EXPLOSION_ANIMATING);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.TrapProps, Trap.NUM_TRAP);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.TriggerTrapProps, Trap.NUM_TRIGGER_TRAP);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.FeedbackTrapProps, Trap.NUM_FEEDBACKS_TRAP);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.SecretTrapProps, Trap.NUM_SECRET_TRAP);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ContainerProps, Container.NUM_CONTAINER);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.ActualContainerProps, Container.NUM_ACTUAL_CONTAINER);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.WasteContainerProps, Container.NUM_WASTE_CONTAINER);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.LiquidContainerProps, Container.NUM_LIQUID_CONTAINER);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.MutantCorpseContainerProps, Container.NUM_MUTANT_CORPSE_CONTAINER);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.RobotCorpseContainerProps, Container.NUM_ROBOT_CORPSE_CONTAINER);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.CyborgCorpseContainerProps, Container.NUM_CYBORG_CORPSE_CONTAINER);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.OtherCorpseContainerProps, Container.NUM_OTHER_CORPSE_CONTAINER);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.EnemyProps, Enemy.NUM_CRITTER);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.MutantEnemyProps, Enemy.NUM_MUTANT_CRITTER);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.RobotEnemyProps, Enemy.NUM_ROBOT_CRITTER);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.CyborgEnemyProps, Enemy.NUM_CYBORG_CRITTER);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.CyberspaceEnemyProps, Enemy.NUM_CYBER_CRITTER);
        ReadData(blobBuilder, binaryReader, ref objectDataBlob.BossEnemyProps, Enemy.NUM_ROBOBABE_CRITTER);

        ReadData(blobBuilder, binaryReader, ref objectDataBlob.BaseProps, Base.NUM_OBJECT);

        var ObjectBase = blobBuilder.Allocate(ref objectDataBlob.ObjectBase, 0x0F << 3);
        var ClassBase = blobBuilder.Allocate(ref objectDataBlob.ClassBase, 0x0F << 3);

        ushort totalCount = 0;
        for (var classIndex = 0; classIndex < ObjectDeclarations.Length; ++classIndex) {
          var subclassDeclaration = ObjectDeclarations[classIndex];

          ushort perClassCount = 0;
          for (var subclassIndex = 0; subclassIndex < subclassDeclaration.Length; ++subclassIndex) {
            ObjectBase[(classIndex << 3) + subclassIndex] = totalCount;
            ClassBase[(classIndex << 3) + subclassIndex] = perClassCount;

            totalCount += (byte)subclassDeclaration[subclassIndex].Count;
            perClassCount += (byte)subclassDeclaration[subclassIndex].Count;
          }
        }

        ObjectDatasBlobAsset = blobBuilder.CreateBlobAssetReference<ObjectDatas>(Allocator.Persistent);
      }
    }
    
    private unsafe void ReadData<T> (in BlobBuilder blobBuilder, BinaryReader binaryReader, ref BlobArray<T> targetBlobArray, int elementCount) where T : struct {
      var blobArray = blobBuilder.Allocate(ref targetBlobArray, elementCount);
      var realBytes = binaryReader.ReadBytes(UnsafeUtility.SizeOf<T>() * elementCount);
      
      UnsafeUtility.MemCpy(blobArray.GetUnsafePtr(), UnsafeUtility.PinGCArrayAndGetDataAddress(realBytes, out ulong gcHandle), realBytes.LongLength);
      UnsafeUtility.ReleaseGCObject(gcHandle);

      // TODO FIXME https://issuetracker.unity3d.com/issues/the-binaryreader-read-data-to-a-span-is-always-zero
      
      // var span = new Span<byte>(blobArray.GetUnsafePtr(), UnsafeUtility.SizeOf<T>() * elementCount);
      // var bytes = binaryReader.Read(span);
    }

    public int BasePropertyIndex(Triple triple) => ObjectDatasBlobAsset.Value.BasePropertyIndex(triple);
    public int ClassPropertyIndex(Triple triple) => ObjectDatasBlobAsset.Value.ClassPropertyIndex(triple);

    public Base BasePropertyData(Triple triple) => ObjectDatasBlobAsset.Value.BasePropertyData(triple);
    public Base BasePropertyData(int index) => ObjectDatasBlobAsset.Value.BasePropertyData(index);

    public void Dispose() {
      ObjectDatasBlobAsset.Dispose();
    }

    public static (int Count, Type Class, Type SubClass)[][] ObjectDeclarations = new []{
      new []{ // 00 Weapons
        (Weapon.NUM_PISTOL_GUN,                 typeof(Weapon), typeof(Weapon.Pistol)),
        (Weapon.NUM_AUTO_GUN,                   typeof(Weapon), typeof(Weapon.Automatic)),
        (Weapon.NUM_SPECIAL_GUN,                typeof(Weapon), typeof(Weapon.Projectile)),
        (Weapon.NUM_HANDTOHAND_GUN,             typeof(Weapon), typeof(Weapon.Melee)),
        (Weapon.NUM_BEAM_GUN,                   typeof(Weapon), typeof(Weapon.Beam)),
        (Weapon.NUM_BEAMPROJ_GUN,               typeof(Weapon), typeof(Weapon.EnergyProjectile))
      },
      new []{ // 01 Ammunition
        (Ammunition.NUM_PISTOL_AMMO,            typeof(Ammunition), typeof(Ammunition.Pistol)),
        (Ammunition.NUM_NEEDLE_AMMO,            typeof(Ammunition), typeof(Ammunition.Needle)),
        (Ammunition.NUM_MAGNUM_AMMO,            typeof(Ammunition), typeof(Ammunition.Magnum)),
        (Ammunition.NUM_RIFLE_AMMO,             typeof(Ammunition), typeof(Ammunition.Rifle)),
        (Ammunition.NUM_FLECHETTE_AMMO,         typeof(Ammunition), typeof(Ammunition.Flechette)),
        (Ammunition.NUM_AUTO_AMMO,              typeof(Ammunition), typeof(Ammunition.Auto)),
        (Ammunition.NUM_PROJ_AMMO,              typeof(Ammunition), typeof(Ammunition.Projectile))
      },
      new []{ // 02 Projectiles
        (Projectile.NUM_TRACER_PHYSICS,         typeof(Projectile), typeof(Projectile.Tracer)),
        (Projectile.NUM_SLOW_PHYSICS,           typeof(Projectile), typeof(Projectile.Slow)),
        (Projectile.NUM_CAMERA_PHYSICS,         typeof(Projectile), typeof(Projectile.Camera))
      },
      new []{ // 03 Grenades & Explosives
        (Explosive.NUM_DIRECT_GRENADE,          typeof(Explosive), typeof(Explosive.Direct)),
        (Explosive.NUM_TIMED_GRENADE,           typeof(Explosive), typeof(Explosive.Timed))
      },
      new []{ // 04 Patches
        (DermalPatch.NUM_STATS_DRUG,            typeof(DermalPatch), typeof(DermalPatch.Stats))
      },
      new []{ // 05 Hardware
        (Hardware.NUM_GOGGLE_HARDWARE,          typeof(Hardware), typeof(Hardware.Goggle)),
        (Hardware.NUM_HARDWARE_HARDWARE,        typeof(Hardware), typeof(Hardware.Hard))
      },
      new []{ // 06 Software & Logs
        (Software.NUM_OFFENSE_SOFTWARE,         typeof(Software), typeof(Software.Offense)),
        (Software.NUM_DEFENSE_SOFTWARE,         typeof(Software), typeof(Software.Defense)),
        (Software.NUM_ONESHOT_SOFTWARE,         typeof(Software), typeof(Software.OneShot)),
        (Software.NUM_MISC_SOFTWARE,            typeof(Software), typeof(Software.Misc)),
        (Software.NUM_DATA_SOFTWARE,            typeof(Software), typeof(Software.Data)),
      },
      new []{ // 07 Decorations
        (Decoration.NUM_ELECTRONIC_BIGSTUFF,    typeof(Decoration), typeof(Decoration.Electronic)),
        (Decoration.NUM_FURNISHING_BIGSTUFF,    typeof(Decoration), typeof(Decoration.Furniture)),
        (Decoration.NUM_ONTHEWALL_BIGSTUFF,     typeof(Decoration), typeof(Decoration.OnTheWall)),
        (Decoration.NUM_LIGHT_BIGSTUFF,         typeof(Decoration), typeof(Decoration.Light)),
        (Decoration.NUM_LABGEAR_BIGSTUFF,       typeof(Decoration), typeof(Decoration.LabGear)),
        (Decoration.NUM_TECHNO_BIGSTUFF,        typeof(Decoration), typeof(Decoration.Techno)),
        (Decoration.NUM_DECOR_BIGSTUFF,         typeof(Decoration), typeof(Decoration.Decor)),
        (Decoration.NUM_TERRAIN_BIGSTUFF,       typeof(Decoration), typeof(Decoration.Terrain))
      },
      new []{ // 08 Items
        (Item.NUM_USELESS_SMALLSTUFF,           typeof(Item), typeof(Item.Useless)),
        (Item.NUM_BROKEN_SMALLSTUFF,            typeof(Item), typeof(Item.Broken)),
        (Item.NUM_CORPSELIKE_SMALLSTUFF,        typeof(Item), typeof(Item.Corpse)),
        (Item.NUM_GEAR_SMALLSTUFF,              typeof(Item), typeof(Item.Gear)),
        (Item.NUM_CARDS_SMALLSTUFF,             typeof(Item), typeof(Item.Cards)),
        (Item.NUM_CYBER_SMALLSTUFF,             typeof(Item), typeof(Item.Cyberspace)),
        (Item.NUM_ONTHEWALL_SMALLSTUFF,         typeof(Item), typeof(Item.OnTheWall)),
        (Item.NUM_PLOT_SMALLSTUFF,              typeof(Item), typeof(Item.Plot))
      },
      new []{ // 09 Fixtures (Switches & Panels)
        (Fixture.NUM_CONTROL_FIXTURE,           typeof(Fixture), typeof(Fixture.Control)),
        (Fixture.NUM_RECEPTACLE_FIXTURE,        typeof(Fixture), typeof(Fixture.Receptacle)),
        (Fixture.NUM_TERMINAL_FIXTURE,          typeof(Fixture), typeof(Fixture.Terminal)),
        (Fixture.NUM_PANEL_FIXTURE,             typeof(Fixture), typeof(Fixture.Panel)),
        (Fixture.NUM_VENDING_FIXTURE,           typeof(Fixture), typeof(Fixture.Vending)),
        (Fixture.NUM_CYBER_FIXTURE,             typeof(Fixture), typeof(Fixture.Cyber))
      },
      new []{ // 0A Doors & Gratings
        (DoorAndGrating.NUM_NORMAL_DOOR,        typeof(DoorAndGrating), typeof(DoorAndGrating.Normal)),
        (DoorAndGrating.NUM_DOORWAYS_DOOR,      typeof(DoorAndGrating), typeof(DoorAndGrating.Doorway)),
        (DoorAndGrating.NUM_FORCE_DOOR,         typeof(DoorAndGrating), typeof(DoorAndGrating.Force)),
        (DoorAndGrating.NUM_ELEVATOR_DOOR,      typeof(DoorAndGrating), typeof(DoorAndGrating.Elevator)),
        (DoorAndGrating.NUM_SPECIAL_DOOR,       typeof(DoorAndGrating), typeof(DoorAndGrating.Special))
      },
      new []{ // 0B Animating
        (Animating.NUM_OBJECT_ANIMATING,        typeof(Animating), typeof(Animating.Object)),
        (Animating.NUM_TRANSITORY_ANIMATING,    typeof(Animating), typeof(Animating.Transitory)),
        (Animating.NUM_EXPLOSION_ANIMATING,     typeof(Animating), typeof(Animating.Explosion))
      },
      new []{ // 0C Traps & Triggers
        (Trap.NUM_TRIGGER_TRAP,                 typeof(Trap), typeof(Trap.Trigger)),
        (Trap.NUM_FEEDBACKS_TRAP,               typeof(Trap), typeof(Trap.Feedback)),
        (Trap.NUM_SECRET_TRAP,                  typeof(Trap), typeof(Trap.Secret)),
      },
      new []{ // 0D Containers
        (Container.NUM_ACTUAL_CONTAINER,        typeof(Container), typeof(Container.Actual)),
        (Container.NUM_WASTE_CONTAINER,         typeof(Container), typeof(Container.Waste)),
        (Container.NUM_LIQUID_CONTAINER,        typeof(Container), typeof(Container.Liquid)),
        (Container.NUM_MUTANT_CORPSE_CONTAINER, typeof(Container), typeof(Container.MutantCorpse)),
        (Container.NUM_ROBOT_CORPSE_CONTAINER,  typeof(Container), typeof(Container.RobotCorpse)),
        (Container.NUM_CYBORG_CORPSE_CONTAINER, typeof(Container), typeof(Container.CyborgCorpse)),
        (Container.NUM_OTHER_CORPSE_CONTAINER,  typeof(Container), typeof(Container.OtherCorpse))
      },
      new []{ // 0E Enemies
        (Enemy.NUM_MUTANT_CRITTER,              typeof(Enemy), typeof(Enemy.Mutant)),
        (Enemy.NUM_ROBOT_CRITTER,               typeof(Enemy), typeof(Enemy.Robot)),
        (Enemy.NUM_CYBORG_CRITTER,              typeof(Enemy), typeof(Enemy.Cyborg)),
        (Enemy.NUM_CYBER_CRITTER,               typeof(Enemy), typeof(Enemy.Cyberspace)),
        (Enemy.NUM_ROBOBABE_CRITTER,            typeof(Enemy), typeof(Enemy.Boss))
      }
    };
  }

  
  [StructLayout(LayoutKind.Sequential)]
  public struct ObjectDatas {
    public BlobArray<Weapon> WeaponProps;
    public BlobArray<Weapon.Pistol> PistolWeaponProps;
    public BlobArray<Weapon.Automatic> AutomaticWeaponProps;
    public BlobArray<Weapon.Projectile> ProjectileWeaponProps;
    public BlobArray<Weapon.Melee> MeleeWeaponProps;
    public BlobArray<Weapon.Beam> BeamWeaponProps;
    public BlobArray<Weapon.EnergyProjectile> EnergyProjectileWeaponProps;

    public BlobArray<Ammunition> AmmunitionProps;
    public BlobArray<Ammunition.Pistol> PistolAmmunitionProps;
    public BlobArray<Ammunition.Needle> NeedleAmmunitionProps;
    public BlobArray<Ammunition.Magnum> MagnumAmmunitionProps;
    public BlobArray<Ammunition.Rifle> RifleAmmunitionProps;
    public BlobArray<Ammunition.Flechette> FlechetteAmmunitionProps;
    public BlobArray<Ammunition.Auto> AutoAmmunitionProps;
    public BlobArray<Ammunition.Projectile> ProjectileAmmunitionProps;

    public BlobArray<Projectile> ProjectileProps;
    public BlobArray<Projectile.Tracer> TracerProjectileProps;
    public BlobArray<Projectile.Slow> SlowProjectileProps;
    public BlobArray<Projectile.Camera> CameraProjectileProps;

    public BlobArray<Explosive> ExplosiveProps;
    public BlobArray<Explosive.Direct> DirectExplosiveProps;
    public BlobArray<Explosive.Timed> TimedExplosiveProps;

    public BlobArray<DermalPatch> DrugProps;
    public BlobArray<DermalPatch.Stats> StatsDrugProps;

    public BlobArray<Hardware> HardwareProps;
    public BlobArray<Hardware.Goggle> GoggleHardwareProps;
    public BlobArray<Hardware.Hard> HardHardwareProps;

    public BlobArray<Software> SoftwareProps;
    public BlobArray<Software.Offense> OffenseSoftwareProps;
    public BlobArray<Software.Defense> DefenseSoftwareProps;
    public BlobArray<Software.OneShot> OneShotSoftwareProps;
    public BlobArray<Software.Misc> MiscSoftwareProps;
    public BlobArray<Software.Data> DataSoftwareProps;

    public BlobArray<Decoration> DecorationProps;
    public BlobArray<Decoration.Electronic> ElectronicDecorationProps;
    public BlobArray<Decoration.Furniture> FurnitureDecorationProps;
    public BlobArray<Decoration.OnTheWall> OnTheWallDecorationProps;
    public BlobArray<Decoration.Light> LightDecorationProps;
    public BlobArray<Decoration.LabGear> LabGearDecorationProps;
    public BlobArray<Decoration.Techno> TechnoDecorationProps;
    public BlobArray<Decoration.Decor> DecorDecorationProps;
    public BlobArray<Decoration.Terrain> TerrainDecorationProps;

    public BlobArray<Item> ItemProps;
    public BlobArray<Item.Useless> UselessItemProps;
    public BlobArray<Item.Broken> BrokenItemProps;
    public BlobArray<Item.Corpse> CorpseItemProps;
    public BlobArray<Item.Gear> GearItemProps;
    public BlobArray<Item.Cards> CardsItemProps;
    public BlobArray<Item.Cyberspace> CyberspaceItemProps;
    public BlobArray<Item.OnTheWall> OnTheWallItemProps;
    public BlobArray<Item.Plot> PlotItemProps;

    public BlobArray<Fixture> FixtureProps;
    public BlobArray<Fixture.Control> ControlFixtureProps;
    public BlobArray<Fixture.Receptacle> ReceptacleFixtureProps;
    public BlobArray<Fixture.Terminal> TerminalFixtureProps;
    public BlobArray<Fixture.Panel> PanelFixtureProps;
    public BlobArray<Fixture.Vending> VendingFixtureProps;
    public BlobArray<Fixture.Cyber> CyberFixtureProps;

    public BlobArray<DoorAndGrating> DoorsAndGratingProps;
    public BlobArray<DoorAndGrating.Normal> NormalDoorsAndGratingProps;
    public BlobArray<DoorAndGrating.Doorway> DoorwayDoorsAndGratingProps;
    public BlobArray<DoorAndGrating.Force> ForceDoorsAndGratingProps;
    public BlobArray<DoorAndGrating.Elevator> ElevatorDoorsAndGratingProps;
    public BlobArray<DoorAndGrating.Special> SpecialDoorsAndGratingProps;

    public BlobArray<Animating> AnimatingProps;
    public BlobArray<Animating.Object> ObjectAnimatingProps;
    public BlobArray<Animating.Transitory> TransitoryAnimatingProps;
    public BlobArray<Animating.Explosion> ExplosionAnimatingProps;

    public BlobArray<Trap> TrapProps;
    public BlobArray<Trap.Trigger> TriggerTrapProps;
    public BlobArray<Trap.Feedback> FeedbackTrapProps;
    public BlobArray<Trap.Secret> SecretTrapProps;

    public BlobArray<Container> ContainerProps;
    public BlobArray<Container.Actual> ActualContainerProps;
    public BlobArray<Container.Waste> WasteContainerProps;
    public BlobArray<Container.Liquid> LiquidContainerProps;
    public BlobArray<Container.MutantCorpse> MutantCorpseContainerProps;
    public BlobArray<Container.RobotCorpse> RobotCorpseContainerProps;
    public BlobArray<Container.CyborgCorpse> CyborgCorpseContainerProps;
    public BlobArray<Container.OtherCorpse> OtherCorpseContainerProps;

    public BlobArray<Enemy> EnemyProps;
    public BlobArray<Enemy.Mutant> MutantEnemyProps;
    public BlobArray<Enemy.Robot> RobotEnemyProps;
    public BlobArray<Enemy.Cyborg> CyborgEnemyProps;
    public BlobArray<Enemy.Cyberspace> CyberspaceEnemyProps;
    public BlobArray<Enemy.Boss> BossEnemyProps;

    public BlobArray<Base> BaseProps;

    public BlobArray<ushort> ObjectBase;
    public BlobArray<ushort> ClassBase;

    public int BasePropertyIndex(Triple triple) => ObjectBase[((byte)triple.Class << 3) + triple.SubClass] + triple.Type;
    public int ClassPropertyIndex(Triple triple) => ClassBase[((byte)triple.Class << 3) + triple.SubClass] + triple.Type;

    public Base BasePropertyData(Triple triple) => BaseProps[BasePropertyIndex(triple)];
    public Base BasePropertyData(int index) => BaseProps[index];
  }
}