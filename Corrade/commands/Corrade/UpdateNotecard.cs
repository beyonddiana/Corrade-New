﻿///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using OpenMetaverse.Assets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;
using Reflection = wasSharp.Reflection;
using Parallel = System.Threading.Tasks.Parallel;
using System.IO;
using System.Text;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> updatenotecard
                =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int)Configuration.Permissions.Inventory))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    // Check all inventory items.
                    var LockObject = new object();
                    var error = Enumerations.ScriptError.NONE;
                    var attachments = new List<InventoryItem>();
                    Parallel.ForEach(CSV.ToEnumerable(wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ATTACHMENTS)),
                                corradeCommandParameters.Message))).Where(o => !string.IsNullOrEmpty(o)), (o, s) =>
                                {
                                    InventoryItem attachmentItem = null;
                                    UUID attachmentUUID;
                                    switch (UUID.TryParse(o, out attachmentUUID))
                                    {
                                        case true:
                                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                                            if (Client.Inventory.Store.Contains(attachmentUUID))
                                            {
                                                attachmentItem = Client.Inventory.Store[attachmentUUID] as InventoryItem;
                                            }
                                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                                            break;

                                        default:
                                            attachmentItem =
                                                Inventory.FindInventory<InventoryItem>(Client, o,
                                                    CORRADE_CONSTANTS.PATH_SEPARATOR,
                                                    CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                                    corradeConfiguration.ServicesTimeout);
                                            break;
                                    }

                                    if (attachmentItem == null)
                                    {
                                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), o);
                                        error = Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND;
                                        s.Break();
                                        return;
                                    }

                                    if (!attachmentItem.Permissions.NextOwnerMask
                                        .HasFlag(PermissionMask.Modify | PermissionMask.Copy | PermissionMask.Transfer))
                                    {
                                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), o);
                                        error = Enumerations.ScriptError.NO_PERMISSIONS_FOR_ITEM;
                                        s.Break();
                                        return;
                                    }

                                    lock (LockObject)
                                    {
                                        attachments.Add(attachmentItem);
                                    }
                                });

                    if (!error.Equals(Enumerations.ScriptError.NONE))
                        throw new Command.ScriptException(error);

                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    var UploadNotecardAssetEvent = new ManualResetEvent(false);
                    bool succeeded = false;
                    Primitive primitive = null;
                    InventoryItem inventoryItem = null;
                    var type = Reflection.GetEnumValueFromName<Enumerations.Type>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                    corradeCommandParameters.Message)));
                    UUID itemUUID;
                    switch (type)
                    {
                        case Enumerations.Type.TASK:
                            if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int)Configuration.Permissions.Interact))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            }
                            float range;
                            if (
                                !float.TryParse(
                                    wasInput(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                        corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                    out range))
                            {
                                range = corradeConfiguration.Range;
                            }
                            var target =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                        corradeCommandParameters.Message));
                            UUID targetUUID;
                            if (!UUID.TryParse(target, out targetUUID))
                            {
                                if (string.IsNullOrEmpty(target))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.NO_TARGET_SPECIFIED);
                                }
                                targetUUID = UUID.Zero;
                            }
                            if (string.IsNullOrEmpty(item))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                            }
                            switch (UUID.TryParse(item, out itemUUID))
                            {
                                case true:
                                    if (
                                        !Services.FindPrimitive(Client,
                                            itemUUID,
                                            range,
                                            ref primitive,
                                            corradeConfiguration.DataTimeout))
                                    {
                                        throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                    }
                                    break;

                                default:
                                    if (
                                        !Services.FindPrimitive(Client,
                                            item,
                                            range,
                                            ref primitive,
                                            corradeConfiguration.DataTimeout))
                                    {
                                        throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                    }
                                    break;
                            }
                            var inventory = new List<InventoryBase>();
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            inventory.AddRange(
                                    Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                        (int)corradeConfiguration.ServicesTimeout));
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                            inventoryItem = !targetUUID.Equals(UUID.Zero)
                                ? inventory.AsParallel().FirstOrDefault(o => o.UUID.Equals(targetUUID)) as InventoryItem
                                : inventory.AsParallel().FirstOrDefault(o => o.Name.Equals(target)) as InventoryItem;
                            // Stop if task inventory does not exist.
                            succeeded = false;
                            if (inventoryItem == null ||
                                !inventoryItem.AssetType.Equals(AssetType.Notecard))
                                throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);

                            break;

                        case Enumerations.Type.AGENT:
                            if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int)Configuration.Permissions.Inventory))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            }
                            // If an item was specified then update instead of creating a new item for certain asset types.
                            if (!string.IsNullOrEmpty(item))
                            {
                                switch (UUID.TryParse(item, out itemUUID))
                                {
                                    case true:
                                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                                        if (Client.Inventory.Store.Contains(itemUUID))
                                        {
                                            inventoryItem = Client.Inventory.Store[itemUUID] as InventoryItem;
                                        }
                                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                                        break;

                                    default:
                                        inventoryItem = Inventory.FindInventory<InventoryBase>(Client, item,
                                            CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                            corradeConfiguration.ServicesTimeout) as InventoryItem;
                                        break;
                                }

                                if (inventoryItem == null || !inventoryItem.AssetType.Equals(AssetType.Notecard))
                                    throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            }

                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_UPDATE_TYPE);
                    }

                    bool temporary = false;
                    if (!bool.TryParse(wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TEMPORARY)),
                            corradeCommandParameters.Message)), out temporary))
                    {
                        temporary = false;
                    }

                    AssetNotecard notecard = null;
                    switch (Reflection.GetEnumValueFromName<Enumerations.Entity>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))))
                    {
                        case Enumerations.Entity.FILE:
                            if (
                                !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                    (int)Configuration.Permissions.System))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            }
                            var path =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(path))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_PATH_PROVIDED);
                            }
                            // Read from file.
                            var data = string.Empty;
                            try
                            {
                                using (
                                    var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                        16384, true))
                                {
                                    using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                    {
                                        data = streamReader.ReadToEnd();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                                throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_READ_FILE);
                            }
                            notecard = new AssetNotecard(inventoryItem.AssetUUID, null)
                            {
                                BodyText = data,
                                EmbeddedItems = attachments,
                                Temporary = temporary
                            };
                            break;

                        case Enumerations.Entity.TEXT:
                            var text =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(text))
                                data = wasOpenMetaverse.Constants.ASSETS.NOTECARD.NEWLINE;

                            notecard = new AssetNotecard(inventoryItem.AssetUUID, null)
                            {
                                BodyText = text,
                                EmbeddedItems = attachments,
                                Temporary = temporary
                            };
                            break;

                        case Enumerations.Entity.ASSET:
                            byte[] asset = null;
                            try
                            {
                                asset = Convert.FromBase64String(
                                    wasInput(
                                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                            corradeCommandParameters.Message)));
                            }
                            catch (Exception)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            }

                            if (asset == null || asset.Length.Equals(0))
                                throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_ASSET_DATA);

                            var assetNotecard = new AssetNotecard()
                            {
                                AssetData = asset
                            };

                            if (!assetNotecard.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);

                            notecard = new AssetNotecard(inventoryItem.AssetUUID, null)
                            {
                                BodyText = assetNotecard.BodyText,
                                EmbeddedItems = attachments,
                                Temporary = temporary
                            };
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }

                    notecard.Encode();

                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        Encoding.UTF8.GetByteCount(notecard.BodyText) >
                        wasOpenMetaverse.Constants.ASSETS.NOTECARD.MAXIMUM_BODY_LENTH)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NOTECARD_MESSAGE_BODY_TOO_LARGE);
                    }

                    switch (type)
                    {
                        case Enumerations.Type.TASK:
                            // Update the notecard inside the task inventory.
                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                            Client.Inventory.RequestUpdateNotecardTask(notecard.AssetData, inventoryItem.UUID, primitive.ID,
                                    delegate (bool completed, string status, UUID itemID, UUID assetID)
                                    {
                                        succeeded = completed;
                                        inventoryItem.UUID = itemID;
                                        inventoryItem.AssetUUID = assetID;
                                        UploadNotecardAssetEvent.Set();
                                    });
                            if (!UploadNotecardAssetEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
                            {
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                            }
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            break;

                        case Enumerations.Type.AGENT:
                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                            Client.Inventory.RequestUploadNotecardAsset(notecard.AssetData, inventoryItem.UUID,
                                    delegate (bool completed, string status, UUID itemID, UUID assetID)
                                    {
                                        succeeded = completed;
                                        inventoryItem.UUID = itemID;
                                        inventoryItem.AssetUUID = assetID;
                                        UploadNotecardAssetEvent.Set();
                                    });
                            if (!UploadNotecardAssetEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
                            {
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                            }
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            break;
                    }

                    // Return the item and asset UUID.
                    result.Add(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                        CSV.FromEnumerable(new[]
                        {
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            inventoryItem.UUID.ToString(),
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ASSET)),
                            inventoryItem.AssetUUID.ToString()
                        }));
                };
        }
    }
}
