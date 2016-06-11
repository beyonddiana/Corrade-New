///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;
using wasOpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> findfolder = (message, rule, senderUUID) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                if (string.IsNullOrEmpty(rule.Option))
                {
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                    }
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
                    Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                    return;
                }
                var folders = new List<string>();
                var parts =
                    new HashSet<string>(rule.Option.Split(RLV_CONSTANTS.AND_OPERATOR.ToCharArray()));
                var LockObject = new object();
                Inventory.FindInventoryPath<InventoryBase>(Client, RLVFolder,
                    CORRADE_CONSTANTS.OneOrMoRegex,
                    new LinkedList<string>())
                    .ToArray()
                    .AsParallel().Where(
                        o =>
                            o.Key is InventoryFolder &&
                            !o.Key.Name.Substring(1).Equals(RLV_CONSTANTS.DOT_MARKER) &&
                            !o.Key.Name.Substring(1).Equals(RLV_CONSTANTS.TILDE_MARKER)).ForAll(o =>
                            {
                                var count = 0;
                                parts.AsParallel().ForAll(p => o.Value.AsParallel().ForAll(q =>
                                {
                                    if (q.Contains(p))
                                    {
                                        Interlocked.Increment(ref count);
                                    }
                                }));
                                if (!count.Equals(parts.Count)) return;
                                lock (LockObject)
                                {
                                    folders.Add(o.Key.Name);
                                }
                            });
                if (folders.Any())
                {
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        Client.Self.Chat(string.Join(RLV_CONSTANTS.PATH_SEPARATOR, folders.ToArray()),
                            channel,
                            ChatType.Normal);
                    }
                }
            };
        }
    }
}