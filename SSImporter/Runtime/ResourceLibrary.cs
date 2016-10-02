using UnityEngine;
using System.Collections.Generic;
using System;

namespace SystemShock.Resource {
    public class ResourceLibrary : AbstractGameController<ResourceLibrary> {

        [SerializeField]
        private AbstractResourceLibrary[] libraries = new AbstractResourceLibrary[0];

#if UNITY_EDITOR
        public void AddLibrary(AbstractResourceLibrary library) {
            int newIndex = libraries.Length;
            Array.Resize(ref libraries, libraries.Length + 1);
            libraries[newIndex] = library;

            this.Save();
        }
#endif
    }
}