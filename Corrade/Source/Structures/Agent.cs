﻿///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Xml.Serialization;
using OpenMetaverse;
using wasSharp;

namespace Corrade.Structures
{
    /// <summary>
    ///     Agent Structure.
    /// </summary>
    [Reflection.NameAttribute("agent")]
    [XmlRoot(ElementName = "Agent")]
    public struct Agent : IEquatable<Agent>
    {
        [Reflection.NameAttribute("firstname")]
        [XmlElement(ElementName = "FirstName")]
        public string FirstName { get; set; }

        [Reflection.NameAttribute("lastname")]
        [XmlElement(ElementName = "LastName")]
        public string LastName { get; set; }

        [Reflection.NameAttribute("UUID")]
        [XmlElement(ElementName = "UUID")]
        public UUID UUID { get; set; }

        public bool Equals(Agent other)
        {
            return (string.Equals(FirstName, other.FirstName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(LastName, other.LastName, StringComparison.OrdinalIgnoreCase)) ||
                   UUID.Equals(other.UUID);
        }

        public override int GetHashCode()
        {
            return NetHash.Init.Hash(UUID);
        }
    }
}
