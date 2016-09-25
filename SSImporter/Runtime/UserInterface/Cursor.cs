using UnityEngine;
using UnityEngine.UI;

using System;
using System.Collections;

using SystemShock.Resource;
using SystemShock.Object;

namespace SystemShock.UserInterface {
    public class Cursor : MonoBehaviour {
        private MessageBus messageBus;
        private SpriteLibrary spriteLibrary;
        private ObjectPropertyLibrary objectPropertyLibrary;

        private Image Icon;
        private RawImage ItemIcon;

        private void Awake() {
            messageBus = MessageBus.GetController();
            spriteLibrary = SpriteLibrary.GetLibrary();
            objectPropertyLibrary = ObjectPropertyLibrary.GetLibrary();
        }

        private void Start() {
            Icon = transform.Find(@"Icon").GetComponent<Image>();
            ItemIcon = transform.Find(@"ItemIcon").GetComponent<RawImage>();

            cursorLocked = false;
            UnityEngine.Cursor.visible = false;

            messageBus.Receive<ObjectInHandMessage>(msg => {
                uint spriteIndex = objectPropertyLibrary.GetSpriteOffset(msg.Payload.CombinedType);

                if (msg.Payload.Class == ObjectClass.Item)
                    spriteIndex += 1;

                SpriteDefinition sprite = spriteLibrary.GetResource(KnownChunkId.ObjectSprites)[spriteIndex];

                itemInHand = true;
                ItemIcon.texture = spriteLibrary.GetAtlas();
                ItemIcon.uvRect = sprite.UVRect;
                ItemIcon.SetNativeSize();
            });
        }

        private void Update() {
            GetComponent<RectTransform>().anchoredPosition = Input.mousePosition;

            if (Input.GetKeyUp(KeyCode.Escape)) {
                UnityEngine.Cursor.visible = !UnityEngine.Cursor.visible;
                if (UnityEngine.Cursor.visible)
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                else
                    cursorLocked = cursorLocked;
            } else if (Input.GetKeyDown(KeyCode.E)) {
                cursorLocked = !cursorLocked;
            }
        }

        private bool itemInHand {
            set { ItemIcon.gameObject.SetActive(value); Icon.gameObject.SetActive(!value); }
            get { return ItemIcon.gameObject.activeSelf; }
        }

        private bool cursorLocked {
            set { UnityEngine.Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None; }
            get { return UnityEngine.Cursor.lockState == CursorLockMode.Locked; }
        }
    }
}