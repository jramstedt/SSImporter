using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace SystemShock.UserInterface {
    public class FilterPhysicsRaycaster : PhysicsRaycaster {
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList) {
            if (eventCamera == null)
                return;

            var ray = eventCamera.ScreenPointToRay(eventData.position);
            float dist = eventCamera.farClipPlane - eventCamera.nearClipPlane;

            var hits = Physics.RaycastAll(ray, dist, finalEventMask);

            if (hits.Length > 1)
                System.Array.Sort(hits, (r1, r2) => r1.distance.CompareTo(r2.distance));

            for (int b = 0, bmax = hits.Length; b < bmax; ++b) {
                IRaycastFilter testable = hits[b].collider.GetComponent<IRaycastFilter>();
                if (testable != null && !testable.TestRaycast(hits[b]))
                    continue;

                var result = new RaycastResult {
                    gameObject = hits[b].collider.gameObject,
                    module = this,
                    distance = hits[b].distance,
                    worldPosition = hits[b].point,
                    worldNormal = hits[b].normal,
                    screenPosition = eventData.position,
                    index = resultAppendList.Count,
                    sortingLayer = 0,
                    sortingOrder = 0
                };

                resultAppendList.Add(result);
            }
        }
    }
}