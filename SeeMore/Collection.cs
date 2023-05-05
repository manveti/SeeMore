using System;

namespace SeeMore {
    [Serializable]
    public class Collection {
        public string name;
        public string description;
        //TODO: thumbnail
        public Guid? parent;

        public Collection(string name, string description, Guid? parent = null) {//TODO: thumbnail
            this.name = name;
            this.description = description;
            this.parent = parent;
        }
    }
}
