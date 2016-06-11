///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setobjectscale =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    bool uniform;
                    if (
                        !bool.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.UNIFORM)),
                                corradeCommandParameters.Message)),
                            out uniform))
                    {
                        uniform = true;
                    }
                    Primitive primitive = null;
                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    UUID itemUUID;
                    switch (UUID.TryParse(item, out itemUUID))
                    {
                        case true:
                            if (
                                !Services.FindObject(Client,
                                    itemUUID,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                            {
                                throw new ScriptException(ScriptError.OBJECT_NOT_FOUND);
                            }
                            break;
                        default:
                            if (
                                !Services.FindObject(Client,
                                    item,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                            {
                                throw new ScriptException(ScriptError.OBJECT_NOT_FOUND);
                            }
                            break;
                    }
                    Simulator simulator;
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        simulator = Client.Network.Simulators.AsParallel()
                            .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle));
                    }
                    if (simulator == null)
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    Vector3 scale;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.SCALE)),
                                    corradeCommandParameters.Message)),
                            out scale))
                    {
                        throw new ScriptException(ScriptError.INVALID_SCALE);
                    }
                    if (Helpers.IsSecondLife(Client) &&
                        (scale.X < Constants.PRIMITIVES.MINIMUM_SIZE_X ||
                         scale.Y < Constants.PRIMITIVES.MINIMUM_SIZE_Y ||
                         scale.Z < Constants.PRIMITIVES.MINIMUM_SIZE_Z ||
                         scale.X > Constants.PRIMITIVES.MAXIMUM_SIZE_X ||
                         scale.Y > Constants.PRIMITIVES.MAXIMUM_SIZE_Y ||
                         scale.Z > Constants.PRIMITIVES.MAXIMUM_SIZE_Z))
                    {
                        throw new ScriptException(ScriptError.SCALE_WOULD_EXCEED_BUILDING_CONSTRAINTS);
                    }
                    lock (Locks.ClientInstanceObjectsLock)
                    {
                        Client.Objects.SetScale(simulator,
                            primitive.LocalID, scale, false, uniform);
                    }
                };
        }
    }
}