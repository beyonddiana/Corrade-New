///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> detach = (message, rule, senderUUID) =>
            {
                if (!rule.Param.Equals(RLV_CONSTANTS.FORCE))
                {
                    return;
                }
                var RLVFolder =
                    Inventory.FindInventory<InventoryNode>(Client, Client.Inventory.Store.RootNode,
                        RLV_CONSTANTS.SHARED_FOLDER_NAME, corradeConfiguration.ServicesTimeout)
                        .ToArray()
                        .AsParallel()
                        .FirstOrDefault(o => o.Data is InventoryFolder);
                if (RLVFolder == null)
                {
                    return;
                }
                switch (!string.IsNullOrEmpty(rule.Option))
                {
                    case true:
                        var RLVattachment =
                            RLVAttachments.AsParallel().FirstOrDefault(
                                o => string.Equals(rule.Option, o.Name, StringComparison.InvariantCultureIgnoreCase));
                        switch (!RLVattachment.Equals(default(RLVAttachment)))
                        {
                            case true: // detach by attachment point
                                Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                    .ToArray()
                                    .AsParallel()
                                    .Where(o => o.Value.Equals(RLVattachment.AttachmentPoint))
                                    .SelectMany(
                                        p =>
                                            Inventory.FindInventory<InventoryBase>(Client,
                                                Client.Inventory.Store.RootNode,
                                                p.Key.Properties.Name, corradeConfiguration.ServicesTimeout)
                                                .ToArray()
                                                .AsParallel().Where(
                                                    o =>
                                                        o is InventoryAttachment || o is InventoryObject))
                                    .Where(o => o != null)
                                    .Select(o => o as InventoryItem).ForAll(o =>
                                    {
                                        var slot = Inventory.GetAttachments(
                                            Client,
                                            corradeConfiguration.DataTimeout)
                                            .ToArray()
                                            .AsParallel()
                                            .Where(
                                                p =>
                                                    p.Key.Properties.ItemID.Equals(
                                                        o.UUID))
                                            .Select(p => p.Value.ToString())
                                            .FirstOrDefault() ?? AttachmentPoint.Default.ToString();
                                        CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                            () => SendNotification(
                                                Configuration.Notifications.OutfitChanged,
                                                new OutfitEventArgs
                                                {
                                                    Action = Action.DETACH,
                                                    Name = o.Name,
                                                    Description = o.Description,
                                                    Item = o.UUID,
                                                    Asset = o.AssetUUID,
                                                    Entity = o.AssetType,
                                                    Creator = o.CreatorID,
                                                    Permissions =
                                                        Inventory.wasPermissionsToString(
                                                            o.Permissions),
                                                    Inventory = o.InventoryType,
                                                    Slot = slot
                                                }),
                                            corradeConfiguration.MaximumNotificationThreads);
                                        Inventory.Detach(Client, CurrentOutfitFolder, o,
                                            corradeConfiguration.ServicesTimeout);
                                    });
                                break;
                            default: // detach by folder(s) name
                                if (string.IsNullOrEmpty(rule.Option)) break;
                                var attachmentInventoryBases =
                                    new List<InventoryBase>(rule.Option.Split(RLV_CONSTANTS.PATH_SEPARATOR[0])
                                        .AsParallel().Select(
                                            p =>
                                                Inventory.FindInventory<InventoryBase>(Client, RLVFolder,
                                                    new Regex(Regex.Escape(p),
                                                        RegexOptions.Compiled | RegexOptions.IgnoreCase),
                                                    corradeConfiguration.ServicesTimeout
                                                    ).AsParallel().FirstOrDefault(o => o is InventoryFolder))
                                        .Where(o => o != null));
                                var wearableAttachments = new List<InventoryBase>();
                                lock (Locks.ClientInstanceInventoryLock)
                                {
                                    wearableAttachments.AddRange(attachmentInventoryBases
                                        .SelectMany(
                                            o =>
                                                Client.Inventory.Store.GetContents(o as InventoryFolder)
                                                    .AsParallel()
                                                    .Where(Inventory.CanBeWorn)));
                                }
                                wearableAttachments.AsParallel().ForAll(o =>
                                {
                                    var inventoryItem = o as InventoryItem;
                                    if (inventoryItem is InventoryWearable)
                                    {
                                        CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                            () => SendNotification(
                                                Configuration.Notifications.OutfitChanged,
                                                new OutfitEventArgs
                                                {
                                                    Action = Action.DETACH,
                                                    Name = inventoryItem.Name,
                                                    Description = inventoryItem.Description,
                                                    Item = inventoryItem.UUID,
                                                    Asset = inventoryItem.AssetUUID,
                                                    Entity = inventoryItem.AssetType,
                                                    Creator = inventoryItem.CreatorID,
                                                    Permissions =
                                                        Inventory.wasPermissionsToString(
                                                            inventoryItem.Permissions),
                                                    Inventory = inventoryItem.InventoryType,
                                                    Slot =
                                                        (inventoryItem as InventoryWearable)
                                                            .WearableType.ToString()
                                                }),
                                            corradeConfiguration.MaximumNotificationThreads);
                                        Inventory.UnWear(Client, CurrentOutfitFolder,
                                            inventoryItem, corradeConfiguration.ServicesTimeout);
                                        return;
                                    }
                                    if (o is InventoryAttachment || o is InventoryObject)
                                    {
                                        var slot = Inventory.GetAttachments(
                                            Client,
                                            corradeConfiguration.DataTimeout)
                                            .ToArray()
                                            .AsParallel()
                                            .Where(
                                                p =>
                                                    p.Key.Properties.ItemID.Equals(
                                                        inventoryItem.UUID))
                                            .Select(p => p.Value.ToString())
                                            .FirstOrDefault() ??
                                                   AttachmentPoint.Default.ToString();
                                        CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                            () => SendNotification(
                                                Configuration.Notifications.OutfitChanged,
                                                new OutfitEventArgs
                                                {
                                                    Action = Action.DETACH,
                                                    Name = inventoryItem.Name,
                                                    Description = inventoryItem.Description,
                                                    Item = inventoryItem.UUID,
                                                    Asset = inventoryItem.AssetUUID,
                                                    Entity = inventoryItem.AssetType,
                                                    Creator = inventoryItem.CreatorID,
                                                    Permissions =
                                                        Inventory.wasPermissionsToString(
                                                            inventoryItem.Permissions),
                                                    Inventory = inventoryItem.InventoryType,
                                                    Slot = slot
                                                }),
                                            corradeConfiguration.MaximumNotificationThreads);
                                        Inventory.Detach(Client, CurrentOutfitFolder, inventoryItem,
                                            corradeConfiguration.ServicesTimeout);
                                    }
                                });
                                RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                                break;
                        }
                        break;
                    default:
                        Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                            .ToArray()
                            .AsParallel()
                            .Where(o => RLVAttachments.Any(p => p.AttachmentPoint.Equals(o.Value)))
                            .SelectMany(
                                o =>
                                    o.Key.NameValues.AsParallel()
                                        .Where(p => string.Equals(@"AttachItemID", p.Name, StringComparison.Ordinal)))
                            .ForAll(
                                o =>
                                {
                                    UUID itemUUID;
                                    if (UUID.TryParse(o.Value.ToString(), out itemUUID))
                                    {
                                        var inventoryItem =
                                            Client.Inventory.Store.Items[itemUUID].Data as InventoryItem;
                                        var slot = Inventory.GetAttachments(
                                            Client,
                                            corradeConfiguration.DataTimeout)
                                            .ToArray()
                                            .AsParallel()
                                            .Where(
                                                p =>
                                                    p.Key.Properties.ItemID.Equals(
                                                        inventoryItem.UUID))
                                            .Select(p => p.Value.ToString())
                                            .FirstOrDefault() ?? AttachmentPoint.Default.ToString();
                                        CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                            () => SendNotification(
                                                Configuration.Notifications.OutfitChanged,
                                                new OutfitEventArgs
                                                {
                                                    Action = Action.DETACH,
                                                    Name = inventoryItem.Name,
                                                    Description = inventoryItem.Description,
                                                    Item = inventoryItem.UUID,
                                                    Asset = inventoryItem.AssetUUID,
                                                    Entity = inventoryItem.AssetType,
                                                    Creator = inventoryItem.CreatorID,
                                                    Permissions =
                                                        Inventory.wasPermissionsToString(
                                                            inventoryItem.Permissions),
                                                    Inventory = inventoryItem.InventoryType,
                                                    Slot = slot
                                                }),
                                            corradeConfiguration.MaximumNotificationThreads);
                                        Inventory.Detach(Client, CurrentOutfitFolder, inventoryItem,
                                            corradeConfiguration.ServicesTimeout);
                                    }
                                });
                        break;
                }
            };
        }
    }
}