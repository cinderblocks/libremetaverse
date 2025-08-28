using System;
using System.Collections.Generic;

namespace LibreMetaverse.RLV
{
    public class RlvSharedFolder
    {
        public Guid Id { get; }
        public string Name { get; set; }
        public RlvSharedFolder? Parent { get; private set; }
        public IReadOnlyList<RlvSharedFolder> Children => _children;
        public IReadOnlyList<RlvInventoryItem> Items => _items;

        private readonly List<RlvSharedFolder> _children;
        private readonly List<RlvInventoryItem> _items;

        public RlvSharedFolder(Guid id, string name)
        {
            Id = id;
            Name = name ?? throw new ArgumentException("Name cannot be null", nameof(name));
            Parent = null;
            _children = [];
            _items = [];
        }

        /// <summary>
        /// Adds a child folder to this folder
        /// </summary>
        /// <param name="id">Folder ID</param>
        /// <param name="name">Folder Name</param>
        /// <returns>Newly added Folder</returns>
        public RlvSharedFolder AddChild(Guid id, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name cannot be null or empty", nameof(name));
            }

            var newChild = new RlvSharedFolder(id, name)
            {
                Parent = this
            };

            _children.Add(newChild);
            return newChild;
        }

        /// <summary>
        /// Adds an item to this folder
        /// </summary>
        /// <param name="id">Item ID</param>
        /// <param name="name">Item Name</param>
        /// <param name="isLink">Item is an item link</param>
        /// <param name="attachedTo">Item attachment point if attached, null if not attached</param>
        /// <param name="attachedPrimId">ID of the attached prim, null if not attached</param>
        /// <param name="wornOn">Item wearable type if worn, null if not a wearable</param>
        /// <param name="gestureState">Gesture state, or null if this is not a gesture</param>
        /// <returns>Newly added item</returns>
        public RlvInventoryItem AddItem(Guid id, string name, bool isLink, RlvAttachmentPoint? attachedTo, Guid? attachedPrimId, RlvWearableType? wornOn, RlvGestureState? gestureState)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name cannot be null or empty", nameof(name));
            }

            var newItem = new RlvInventoryItem(id, name, isLink, this, attachedTo, attachedPrimId, wornOn, gestureState);

            _items.Add(newItem);
            return newItem;
        }

        public override string ToString()
        {
            return $"{Name ?? Id.ToString()} (Children: {_children.Count}, Items: {_items.Count})";
        }
    }
}
