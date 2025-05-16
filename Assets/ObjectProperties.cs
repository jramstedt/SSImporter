using SS.ObjectProperties;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Entities;

namespace SS.Resources {
  public class ObjectProperties : IDisposable {
    private const uint FILE_VERSION = 45;

    public BlobAssetReference<ObjectPropertiesBlob> ObjectDatasBlobAsset;

    public ObjectProperties(byte[] objPropData) {
      using var fileStream = new MemoryStream(objPropData, false);
      using var binaryReader = new BinaryReader(fileStream, Encoding.ASCII);

      uint version = binaryReader.ReadUInt32();
      if (version != FILE_VERSION)
        throw new NotSupportedException($"File version is not supported ({version})");

      var blobBuilder = new BlobBuilder(Allocator.Temp);
      ref var propBlob = ref blobBuilder.ConstructRoot<ObjectPropertiesBlob>();

      binaryReader.Read(ref blobBuilder, ref propBlob.WeaponProps, Weapon.NUM_GUN);
      binaryReader.Read(ref blobBuilder, ref propBlob.PistolWeaponProps, Weapon.NUM_PISTOL_GUN);
      binaryReader.Read(ref blobBuilder, ref propBlob.AutomaticWeaponProps, Weapon.NUM_AUTO_GUN);
      binaryReader.Read(ref blobBuilder, ref propBlob.ProjectileWeaponProps, Weapon.NUM_SPECIAL_GUN);
      binaryReader.Read(ref blobBuilder, ref propBlob.MeleeWeaponProps, Weapon.NUM_HANDTOHAND_GUN);
      binaryReader.Read(ref blobBuilder, ref propBlob.BeamWeaponProps, Weapon.NUM_BEAM_GUN);
      binaryReader.Read(ref blobBuilder, ref propBlob.EnergyProjectileWeaponProps, Weapon.NUM_BEAMPROJ_GUN);

      binaryReader.Read(ref blobBuilder, ref propBlob.AmmunitionProps, Ammunition.NUM_AMMO);
      binaryReader.Read(ref blobBuilder, ref propBlob.PistolAmmunitionProps, Ammunition.NUM_PISTOL_AMMO);
      binaryReader.Read(ref blobBuilder, ref propBlob.NeedleAmmunitionProps, Ammunition.NUM_NEEDLE_AMMO);
      binaryReader.Read(ref blobBuilder, ref propBlob.MagnumAmmunitionProps, Ammunition.NUM_MAGNUM_AMMO);
      binaryReader.Read(ref blobBuilder, ref propBlob.RifleAmmunitionProps, Ammunition.NUM_RIFLE_AMMO);
      binaryReader.Read(ref blobBuilder, ref propBlob.FlechetteAmmunitionProps, Ammunition.NUM_FLECHETTE_AMMO);
      binaryReader.Read(ref blobBuilder, ref propBlob.AutoAmmunitionProps, Ammunition.NUM_AUTO_AMMO);
      binaryReader.Read(ref blobBuilder, ref propBlob.ProjectileAmmunitionProps, Ammunition.NUM_PROJ_AMMO);

      binaryReader.Read(ref blobBuilder, ref propBlob.ProjectileProps, Projectile.NUM_PHYSICS);
      binaryReader.Read(ref blobBuilder, ref propBlob.TracerProjectileProps, Projectile.NUM_TRACER_PHYSICS);
      binaryReader.Read(ref blobBuilder, ref propBlob.SlowProjectileProps, Projectile.NUM_SLOW_PHYSICS);
      binaryReader.Read(ref blobBuilder, ref propBlob.CameraProjectileProps, Projectile.NUM_CAMERA_PHYSICS);

      binaryReader.Read(ref blobBuilder, ref propBlob.ExplosiveProps, Explosive.NUM_GRENADE);
      binaryReader.Read(ref blobBuilder, ref propBlob.DirectExplosiveProps, Explosive.NUM_DIRECT_GRENADE);
      binaryReader.Read(ref blobBuilder, ref propBlob.TimedExplosiveProps, Explosive.NUM_TIMED_GRENADE);

      binaryReader.Read(ref blobBuilder, ref propBlob.DrugProps, DermalPatch.NUM_DRUG);
      binaryReader.Read(ref blobBuilder, ref propBlob.StatsDrugProps, DermalPatch.NUM_STATS_DRUG);

      binaryReader.Read(ref blobBuilder, ref propBlob.HardwareProps, Hardware.NUM_HARDWARE);
      binaryReader.Read(ref blobBuilder, ref propBlob.GoggleHardwareProps, Hardware.NUM_GOGGLE_HARDWARE);
      binaryReader.Read(ref blobBuilder, ref propBlob.HardHardwareProps, Hardware.NUM_HARDWARE_HARDWARE);

      binaryReader.Read(ref blobBuilder, ref propBlob.SoftwareProps, Software.NUM_SOFTWARE);
      binaryReader.Read(ref blobBuilder, ref propBlob.OffenseSoftwareProps, Software.NUM_OFFENSE_SOFTWARE);
      binaryReader.Read(ref blobBuilder, ref propBlob.DefenseSoftwareProps, Software.NUM_DEFENSE_SOFTWARE);
      binaryReader.Read(ref blobBuilder, ref propBlob.OneShotSoftwareProps, Software.NUM_ONESHOT_SOFTWARE);
      binaryReader.Read(ref blobBuilder, ref propBlob.MiscSoftwareProps, Software.NUM_MISC_SOFTWARE);
      binaryReader.Read(ref blobBuilder, ref propBlob.DataSoftwareProps, Software.NUM_DATA_SOFTWARE);

      binaryReader.Read(ref blobBuilder, ref propBlob.DecorationProps, Decoration.NUM_BIGSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.ElectronicDecorationProps, Decoration.NUM_ELECTRONIC_BIGSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.FurnitureDecorationProps, Decoration.NUM_FURNISHING_BIGSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.OnTheWallDecorationProps, Decoration.NUM_ONTHEWALL_BIGSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.LightDecorationProps, Decoration.NUM_LIGHT_BIGSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.LabGearDecorationProps, Decoration.NUM_LABGEAR_BIGSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.TechnoDecorationProps, Decoration.NUM_TECHNO_BIGSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.DecorDecorationProps, Decoration.NUM_DECOR_BIGSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.TerrainDecorationProps, Decoration.NUM_TERRAIN_BIGSTUFF);

      binaryReader.Read(ref blobBuilder, ref propBlob.ItemProps, Item.NUM_SMALLSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.UselessItemProps, Item.NUM_USELESS_SMALLSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.BrokenItemProps, Item.NUM_BROKEN_SMALLSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.CorpseItemProps, Item.NUM_CORPSELIKE_SMALLSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.GearItemProps, Item.NUM_GEAR_SMALLSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.CardsItemProps, Item.NUM_CARDS_SMALLSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.CyberspaceItemProps, Item.NUM_CYBER_SMALLSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.OnTheWallItemProps, Item.NUM_ONTHEWALL_SMALLSTUFF);
      binaryReader.Read(ref blobBuilder, ref propBlob.PlotItemProps, Item.NUM_PLOT_SMALLSTUFF);

      binaryReader.Read(ref blobBuilder, ref propBlob.FixtureProps, Fixture.NUM_FIXTURE);
      binaryReader.Read(ref blobBuilder, ref propBlob.ControlFixtureProps, Fixture.NUM_CONTROL_FIXTURE);
      binaryReader.Read(ref blobBuilder, ref propBlob.ReceptacleFixtureProps, Fixture.NUM_RECEPTACLE_FIXTURE);
      binaryReader.Read(ref blobBuilder, ref propBlob.TerminalFixtureProps, Fixture.NUM_TERMINAL_FIXTURE);
      binaryReader.Read(ref blobBuilder, ref propBlob.PanelFixtureProps, Fixture.NUM_PANEL_FIXTURE);
      binaryReader.Read(ref blobBuilder, ref propBlob.VendingFixtureProps, Fixture.NUM_VENDING_FIXTURE);
      binaryReader.Read(ref blobBuilder, ref propBlob.CyberFixtureProps, Fixture.NUM_CYBER_FIXTURE);

      binaryReader.Read(ref blobBuilder, ref propBlob.DoorsAndGratingProps, DoorAndGrating.NUM_DOOR);
      binaryReader.Read(ref blobBuilder, ref propBlob.NormalDoorsAndGratingProps, DoorAndGrating.NUM_NORMAL_DOOR);
      binaryReader.Read(ref blobBuilder, ref propBlob.DoorwayDoorsAndGratingProps, DoorAndGrating.NUM_DOORWAYS_DOOR);
      binaryReader.Read(ref blobBuilder, ref propBlob.ForceDoorsAndGratingProps, DoorAndGrating.NUM_FORCE_DOOR);
      binaryReader.Read(ref blobBuilder, ref propBlob.ElevatorDoorsAndGratingProps, DoorAndGrating.NUM_ELEVATOR_DOOR);
      binaryReader.Read(ref blobBuilder, ref propBlob.SpecialDoorsAndGratingProps, DoorAndGrating.NUM_SPECIAL_DOOR);

      binaryReader.Read(ref blobBuilder, ref propBlob.AnimatingProps, Animating.NUM_ANIMATING);
      binaryReader.Read(ref blobBuilder, ref propBlob.ObjectAnimatingProps, Animating.NUM_OBJECT_ANIMATING);
      binaryReader.Read(ref blobBuilder, ref propBlob.TransitoryAnimatingProps, Animating.NUM_TRANSITORY_ANIMATING);
      binaryReader.Read(ref blobBuilder, ref propBlob.ExplosionAnimatingProps, Animating.NUM_EXPLOSION_ANIMATING);

      binaryReader.Read(ref blobBuilder, ref propBlob.TrapProps, Trap.NUM_TRAP);
      binaryReader.Read(ref blobBuilder, ref propBlob.TriggerTrapProps, Trap.NUM_TRIGGER_TRAP);
      binaryReader.Read(ref blobBuilder, ref propBlob.FeedbackTrapProps, Trap.NUM_FEEDBACKS_TRAP);
      binaryReader.Read(ref blobBuilder, ref propBlob.SecretTrapProps, Trap.NUM_SECRET_TRAP);

      binaryReader.Read(ref blobBuilder, ref propBlob.ContainerProps, Container.NUM_CONTAINER);
      binaryReader.Read(ref blobBuilder, ref propBlob.ActualContainerProps, Container.NUM_ACTUAL_CONTAINER);
      binaryReader.Read(ref blobBuilder, ref propBlob.WasteContainerProps, Container.NUM_WASTE_CONTAINER);
      binaryReader.Read(ref blobBuilder, ref propBlob.LiquidContainerProps, Container.NUM_LIQUID_CONTAINER);
      binaryReader.Read(ref blobBuilder, ref propBlob.MutantCorpseContainerProps, Container.NUM_MUTANT_CORPSE_CONTAINER);
      binaryReader.Read(ref blobBuilder, ref propBlob.RobotCorpseContainerProps, Container.NUM_ROBOT_CORPSE_CONTAINER);
      binaryReader.Read(ref blobBuilder, ref propBlob.CyborgCorpseContainerProps, Container.NUM_CYBORG_CORPSE_CONTAINER);
      binaryReader.Read(ref blobBuilder, ref propBlob.OtherCorpseContainerProps, Container.NUM_OTHER_CORPSE_CONTAINER);

      binaryReader.Read(ref blobBuilder, ref propBlob.EnemyProps, Enemy.NUM_CRITTER);
      binaryReader.Read(ref blobBuilder, ref propBlob.MutantEnemyProps, Enemy.NUM_MUTANT_CRITTER);
      binaryReader.Read(ref blobBuilder, ref propBlob.RobotEnemyProps, Enemy.NUM_ROBOT_CRITTER);
      binaryReader.Read(ref blobBuilder, ref propBlob.CyborgEnemyProps, Enemy.NUM_CYBORG_CRITTER);
      binaryReader.Read(ref blobBuilder, ref propBlob.CyberspaceEnemyProps, Enemy.NUM_CYBER_CRITTER);
      binaryReader.Read(ref blobBuilder, ref propBlob.BossEnemyProps, Enemy.NUM_ROBOBABE_CRITTER);

      binaryReader.Read(ref blobBuilder, ref propBlob.BaseProps, Base.NUM_OBJECT);

      var ObjectBase = blobBuilder.Allocate(ref propBlob.ObjectBase, 0x0F << 3);
      var ClassBase = blobBuilder.Allocate(ref propBlob.ClassBase, 0x0F << 3);

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
      
      ObjectDatasBlobAsset = blobBuilder.CreateBlobAssetReference<ObjectPropertiesBlob>(Allocator.Persistent);
      
      blobBuilder.Dispose();
    }

    public int BasePropertyIndex(Triple triple) => ObjectDatasBlobAsset.Value.BasePropertyIndex(triple);
    public int ClassPropertyIndex(Triple triple) => ObjectDatasBlobAsset.Value.ClassPropertyIndex(triple);

    public Base BasePropertyData(Triple triple) => ObjectDatasBlobAsset.Value.BasePropertyData(triple);
    public Base BasePropertyData(int baseIndex) => ObjectDatasBlobAsset.Value.BasePropertyData(baseIndex);

    public Enemy EnemyPropertyData(Triple triple) => ObjectDatasBlobAsset.Value.EnemyPropertyData(triple);
    public Enemy EnemyPropertyData(int classIndex) => ObjectDatasBlobAsset.Value.EnemyProps[classIndex];

    public void Dispose() {
      ObjectDatasBlobAsset.Dispose();
    }

    public static readonly (int Count, Type Class, Type SubClass)[][] ObjectDeclarations = {
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
  public struct ObjectPropertiesBlob {
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
    public Base BasePropertyData(int baseIndex) => BaseProps[baseIndex];

    // Subclass properties
    public Animating AnimatingPropertyData(Triple triple) => AnimatingProps[ClassPropertyIndex(triple)];
    public Enemy EnemyPropertyData(Triple triple) => EnemyProps[ClassPropertyIndex(triple)];
  }
}