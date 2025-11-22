using System;
using System.Collections.Generic;
using System.Text;

namespace OpenMetaverse
{
    public partial class InventoryManager
    {
        /// <summary>
        /// Callback for inventory item creation finishing
        /// </summary>
        /// <param name="success">Whether the request to create an inventory
        /// item succeeded or not</param>
        /// <param name="item">Inventory item being created. If success is
        /// false this will be null</param>
        public delegate void ItemCreatedCallback(bool success, InventoryItem item);

        /// <summary>
        /// Callback for an inventory item being created from an uploaded asset
        /// </summary>
        /// <param name="success">true if inventory item creation was successful</param>
        /// <param name="status"></param>
        /// <param name="itemID"></param>
        /// <param name="assetID"></param>
        public delegate void ItemCreatedFromAssetCallback(bool success, string status, UUID itemID, UUID assetID);

        /// <summary>
        /// Callback for inventory item copy
        /// </summary>
        /// <param name="item"></param>
        public delegate void ItemCopiedCallback(InventoryBase item);

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<ItemReceivedEventArgs> m_ItemReceived;

        ///<summary>Raises the ItemReceived Event</summary>
        /// <param name="e">A ItemReceivedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnItemReceived(ItemReceivedEventArgs e)
        {
            EventHandler<ItemReceivedEventArgs> handler = m_ItemReceived;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ItemReceivedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<ItemReceivedEventArgs> ItemReceived
        {
            add { lock (m_ItemReceivedLock) { m_ItemReceived += value; } }
            remove { lock (m_ItemReceivedLock) { m_ItemReceived -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<FolderUpdatedEventArgs> m_FolderUpdated;

        ///<summary>Raises the FolderUpdated Event</summary>
        /// <param name="e">A FolderUpdatedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnFolderUpdated(FolderUpdatedEventArgs e)
        {
            EventHandler<FolderUpdatedEventArgs> handler = m_FolderUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FolderUpdatedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<FolderUpdatedEventArgs> FolderUpdated
        {
            add { lock (m_FolderUpdatedLock) { m_FolderUpdated += value; } }
            remove { lock (m_FolderUpdatedLock) { m_FolderUpdated -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<InventoryObjectOfferedEventArgs> m_InventoryObjectOffered;

        ///<summary>Raises the InventoryObjectOffered Event</summary>
        /// <param name="e">A InventoryObjectOfferedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnInventoryObjectOffered(InventoryObjectOfferedEventArgs e)
        {
            EventHandler<InventoryObjectOfferedEventArgs> handler = m_InventoryObjectOffered;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_InventoryObjectOfferedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// an inventory object sent by another avatar or primitive</summary>
        public event EventHandler<InventoryObjectOfferedEventArgs> InventoryObjectOffered
        {
            add { lock (m_InventoryObjectOfferedLock) { m_InventoryObjectOffered += value; } }
            remove { lock (m_InventoryObjectOfferedLock) { m_InventoryObjectOffered -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<TaskItemReceivedEventArgs> m_TaskItemReceived;

        ///<summary>Raises the TaskItemReceived Event</summary>
        /// <param name="e">A TaskItemReceivedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnTaskItemReceived(TaskItemReceivedEventArgs e)
        {
            EventHandler<TaskItemReceivedEventArgs> handler = m_TaskItemReceived;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_TaskItemReceivedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<TaskItemReceivedEventArgs> TaskItemReceived
        {
            add { lock (m_TaskItemReceivedLock) { m_TaskItemReceived += value; } }
            remove { lock (m_TaskItemReceivedLock) { m_TaskItemReceived -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<FindObjectByPathReplyEventArgs> m_FindObjectByPathReply;

        ///<summary>Raises the FindObjectByPath Event</summary>
        /// <param name="e">A FindObjectByPathEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnFindObjectByPathReply(FindObjectByPathReplyEventArgs e)
        {
            EventHandler<FindObjectByPathReplyEventArgs> handler = m_FindObjectByPathReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FindObjectByPathReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<FindObjectByPathReplyEventArgs> FindObjectByPathReply
        {
            add { lock (m_FindObjectByPathReplyLock) { m_FindObjectByPathReply += value; } }
            remove { lock (m_FindObjectByPathReplyLock) { m_FindObjectByPathReply -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<TaskInventoryReplyEventArgs> m_TaskInventoryReply;

        ///<summary>Raises the TaskInventoryReply Event</summary>
        /// <param name="e">A TaskInventoryReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnTaskInventoryReply(TaskInventoryReplyEventArgs e)
        {
            EventHandler<TaskInventoryReplyEventArgs> handler = m_TaskInventoryReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_TaskInventoryReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<TaskInventoryReplyEventArgs> TaskInventoryReply
        {
            add { lock (m_TaskInventoryReplyLock) { m_TaskInventoryReply += value; } }
            remove { lock (m_TaskInventoryReplyLock) { m_TaskInventoryReply -= value; } }
        }

        /// <summary>
        /// Reply received when uploading an inventory asset
        /// </summary>
        /// <param name="success">Has upload been successful</param>
        /// <param name="status">Error message if upload failed</param>
        /// <param name="itemID">Inventory asset UUID</param>
        /// <param name="assetID">New asset UUID</param>
        public delegate void InventoryUploadedAssetCallback(bool success, string status, UUID itemID, UUID assetID);

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<SaveAssetToInventoryEventArgs> m_SaveAssetToInventory;

        ///<summary>Raises the SaveAssetToInventory Event</summary>
        /// <param name="e">A SaveAssetToInventoryEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnSaveAssetToInventory(SaveAssetToInventoryEventArgs e)
        {
            EventHandler<SaveAssetToInventoryEventArgs> handler = m_SaveAssetToInventory;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_SaveAssetToInventoryLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<SaveAssetToInventoryEventArgs> SaveAssetToInventory
        {
            add { lock (m_SaveAssetToInventoryLock) { m_SaveAssetToInventory += value; } }
            remove { lock (m_SaveAssetToInventoryLock) { m_SaveAssetToInventory -= value; } }
        }

        /// <summary>
        /// Delegate that is invoked when script upload is completed
        /// </summary>
        /// <param name="uploadSuccess">Has upload succeeded (note, there still might be compiler errors)</param>
        /// <param name="uploadStatus">Upload status message</param>
        /// <param name="compileSuccess">Is compilation successful</param>
        /// <param name="compileMessages">If compilation failed, list of error messages, null on compilation success</param>
        /// <param name="itemID">Script inventory UUID</param>
        /// <param name="assetID">Script's new asset UUID</param>
        public delegate void ScriptUpdatedCallback(bool uploadSuccess, string uploadStatus, bool compileSuccess, List<string> compileMessages, UUID itemID, UUID assetID);

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<ScriptRunningReplyEventArgs> m_ScriptRunningReply;

        ///<summary>Raises the ScriptRunningReply Event</summary>
        /// <param name="e">A ScriptRunningReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnScriptRunningReply(ScriptRunningReplyEventArgs e)
        {
            EventHandler<ScriptRunningReplyEventArgs> handler = m_ScriptRunningReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ScriptRunningReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<ScriptRunningReplyEventArgs> ScriptRunningReply
        {
            add { lock (m_ScriptRunningReplyLock) { m_ScriptRunningReply += value; } }
            remove { lock (m_ScriptRunningReplyLock) { m_ScriptRunningReply -= value; } }
        }
    }
}
