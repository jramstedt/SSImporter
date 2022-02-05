
using System;
using System.IO;
using System.Text;
using SS.ObjectProperties;
using Unity.Collections;
using Unity.Entities;

namespace SS.Resources {
  public class ObjectProperties : IDisposable {
    private const uint FILE_VERSION = 45;

    private MemoryStream fileStream;
    private BinaryReader binaryReader;

    public ObjectProperties(byte[] objPropData) {
      fileStream = new MemoryStream(objPropData, false);
      binaryReader = new BinaryReader(fileStream, Encoding.ASCII);

      uint version = binaryReader.ReadUInt32();
      if (version != FILE_VERSION)
        throw new NotSupportedException($"File version is not supported ({version})");

      using (var blobBuilder = new BlobBuilder(Allocator.Temp)) {
        for (uint classIndex = 0; classIndex < ObjectDeclarations.Length; ++classIndex) {
          var objectDataSubclass = ObjectDeclarations[classIndex];

          #region Generic properties
          for (uint subclassIndex = 0; subclassIndex < objectDataSubclass.Length; ++subclassIndex) {
            var objectDataType = objectDataSubclass[subclassIndex];

            for (uint typeIndex = 0; typeIndex < objectDataType.Count; ++typeIndex) {
              
                
            }
          }
          #endregion

          #region Specific properties
          for (uint subclassIndex = 0; subclassIndex < objectDataSubclass.Length; ++subclassIndex) {
            var objectDataType = objectDataSubclass[subclassIndex];

            for (uint typeIndex = 0; typeIndex < objectDataType.Count; ++typeIndex) {
              
            }
          }
          #endregion
        }
      }
    }

    public void Dispose() { }

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
        new ObjectDeclaration<Ammunition, Ammunition.Pistol>(2),
        new ObjectDeclaration<Ammunition, Ammunition.Needle>(2),
        new ObjectDeclaration<Ammunition, Ammunition.Magnum>(3),
        new ObjectDeclaration<Ammunition, Ammunition.Rifle>(2),
        new ObjectDeclaration<Ammunition, Ammunition.Flechette>(2),
        new ObjectDeclaration<Ammunition, Ammunition.Auto>(2),
        new ObjectDeclaration<Ammunition, Ammunition.Projectile>(2)
      },
      new ObjectDeclaration[] { // 02 Projectiles
        new ObjectDeclaration<Projectile, Projectile.Tracer>(6),
        new ObjectDeclaration<Projectile, Projectile.Slow>(16),
        new ObjectDeclaration<Projectile, Projectile.Camera>(2)
      },
      new ObjectDeclaration[] { // 03 Grenades & Explosives
        new ObjectDeclaration<Explosive, Explosive.Grenade>(5),
        new ObjectDeclaration<Explosive, Explosive.Bomb>(3)
      },
      new ObjectDeclaration[] { // 04 Patches
        new ObjectDeclaration<DermalPatch, DermalPatch.All>(7)
      },
      new ObjectDeclaration[] { // 05 Hardware
        new ObjectDeclaration<Hardware, Hardware.Goggle>(5),
        new ObjectDeclaration<Hardware, Hardware.Hard>(10)
      },
      new ObjectDeclaration[] { // 06 Software & Logs
        new ObjectDeclaration<Software, Software.Offence>(7),
        new ObjectDeclaration<Software, Software.Defense>(3),
        new ObjectDeclaration<Software, Software.OneShot>(4),
        new ObjectDeclaration<Software, Software.Misc>(5),
        new ObjectDeclaration<Software, Software.Data>(3),
      },
      new ObjectDeclaration[] { // 07 Decorations
        new ObjectDeclaration<Decoration, Decoration.Electronic>(9),
        new ObjectDeclaration<Decoration, Decoration.Furniture>(10),
        new ObjectDeclaration<Decoration, Decoration.OnTheWall>(11),
        new ObjectDeclaration<Decoration, Decoration.Light>(4),
        new ObjectDeclaration<Decoration, Decoration.LabGear>(9),
        new ObjectDeclaration<Decoration, Decoration.Techno>(8),
        new ObjectDeclaration<Decoration, Decoration.Decor>(16),
        new ObjectDeclaration<Decoration, Decoration.Terrain>(10)
      },
      new ObjectDeclaration[] { // 08 Items
        new ObjectDeclaration<Item, Item.Useless>(8),
        new ObjectDeclaration<Item, Item.Broken>(10),
        new ObjectDeclaration<Item, Item.Corpse>(15),
        new ObjectDeclaration<Item, Item.Gear>(6),
        new ObjectDeclaration<Item, Item.Cards>(12),
        new ObjectDeclaration<Item, Item.Cyberspace>(12),
        new ObjectDeclaration<Item, Item.OnTheWall>(9),
        new ObjectDeclaration<Item, Item.Plot>(8)
      },
      new ObjectDeclaration[] { // 09 Fixtures (Switches & Panels)
        new ObjectDeclaration<Fixture, Fixture.Control>(9),
        new ObjectDeclaration<Fixture, Fixture.Receptacle>(7),
        new ObjectDeclaration<Fixture, Fixture.Terminal>(3),
        new ObjectDeclaration<Fixture, Fixture.Panel>(11),
        new ObjectDeclaration<Fixture, Fixture.Vending>(2),
        new ObjectDeclaration<Fixture, Fixture.Cyber>(3)
      },
      new ObjectDeclaration[] { // 0A Doors & Gratings
        new ObjectDeclaration<DoorAndGrating, DoorAndGrating.Normal>(10),
        new ObjectDeclaration<DoorAndGrating, DoorAndGrating.Doorway>(9),
        new ObjectDeclaration<DoorAndGrating, DoorAndGrating.Force>(7),
        new ObjectDeclaration<DoorAndGrating, DoorAndGrating.Elevator>(5),
        new ObjectDeclaration<DoorAndGrating, DoorAndGrating.Special>(10)
      },
      new ObjectDeclaration[] { // 0B Animating
        new ObjectDeclaration<Animating, Animating.Object>(9),
        new ObjectDeclaration<Animating, Animating.Transitory>(11),
        new ObjectDeclaration<Animating, Animating.Explosion>(14)
      },
      new ObjectDeclaration[] { // 0C Traps & Triggers
        new ObjectDeclaration<Trap, Trap.Trigger>(13),
        new ObjectDeclaration<Trap, Trap.Feedback>(1),
        new ObjectDeclaration<Trap, Trap.Secret>(5),
      },
      new ObjectDeclaration[] { // 0D Containers
        new ObjectDeclaration<Container, Container.Actual>(3),
        new ObjectDeclaration<Container, Container.Waste>(3),
        new ObjectDeclaration<Container, Container.Liquid>(4),
        new ObjectDeclaration<Container, Container.MutantCorpse>(8),
        new ObjectDeclaration<Container, Container.RobotCorpse>(13),
        new ObjectDeclaration<Container, Container.CyborgCorpse>(7),
        new ObjectDeclaration<Container, Container.OtherCorpse>(8)
      },
      new ObjectDeclaration[] { // 0E Enemies
        new ObjectDeclaration<Enemy, Enemy.Mutant>(9),
        new ObjectDeclaration<Enemy, Enemy.Robot>(12),
        new ObjectDeclaration<Enemy, Enemy.Cyborg>(7),
        new ObjectDeclaration<Enemy, Enemy.Cyberspace>(7),
        new ObjectDeclaration<Enemy, Enemy.Boss>(2)
      }
    };
  }

  public abstract class ObjectDeclaration {
    public uint Count;

    public abstract Type Generic { get; }
    public abstract Type Specific { get; }
  }

  public sealed class ObjectDeclaration<G, S> : ObjectDeclaration
    where G : struct
    where S : struct {
    public ObjectDeclaration(uint count) {
      Count = count;
    }

    public override Type Generic => typeof(G);
    public override Type Specific => typeof(S);
  }
}