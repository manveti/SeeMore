using System;

namespace SeeMore {
    [Serializable]
    public class Collection {
        public string name;
        public string description;
        public byte[] icon;
        public Guid? parent;

        public Collection(string name, string description, byte[] icon = null, Guid? parent = null) {
            this.name = name;
            this.description = description;
            this.icon = icon;
            this.parent = parent;
        }
    }
}
