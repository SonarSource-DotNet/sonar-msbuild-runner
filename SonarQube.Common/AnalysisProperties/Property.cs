﻿//-----------------------------------------------------------------------
// <copyright file="Property.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class to describe an additional analysis configuration property
    /// /// </summary>
    /// <remarks>The class is XML-serializable</remarks>
    public class Property
    {
        #region Data

        /// <summary>
        /// The identifier for the property
        /// </summary>
        /// <remarks>Each type </remarks>
        [XmlAttribute("Name")]
        public string Id { get; set; }

        /// <summary>
        /// The value of the property
        /// </summary>
        [XmlText]
        public string Value { get; set; }

        #endregion

        #region Static helper methods
        
        //TODO: this expression only works for single-line values
        // Regular expression pattern: we're looking for matches that:
        // * start at the beginning of a line
        // * start with a character or number
        // * are in the form [key]=[value],
        // * where [key] can  
        //   - starts with an alpanumeric character.
        //   - can be followed by any number of alphanumeric characters or .
        //   - whitespace is not allowed
        // * [value] can contain anything
        public const string KeyValuePropertyPattern = @"^(?<key>\w[\w\d\.-]*)=(?<value>[^\r\n]+)";

        private static readonly Regex SingleLinePropertyRegEx = new Regex(KeyValuePropertyPattern, RegexOptions.Compiled);
        
        /// <summary>
        /// Attempts to parse the supplied string into a key and value
        /// </summary>
        public static bool TryParse(string input, out Property property)
        {
            property = null;

            Match match = SingleLinePropertyRegEx.Match(input);

            if (match.Success)
            {
                string key = match.Groups["key"].Value;
                string value = match.Groups["value"].Value;

                property = new Property() { Id = key, Value = value };
            }
            return property != null;
        }

        /// <summary>
        /// Comparer to use when comparing keys of analysis properties
        /// </summary>
        public static readonly IEqualityComparer<string> PropertyKeyComparer = StringComparer.Ordinal;

        /// <summary>
        /// Returns the first property with the supplied key, or null if there is no match
        /// </summary>
        public static bool TryGetProperty(string key, IEnumerable<Property> properties, out Property property)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException("key");
            }
            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }

            property = properties.FirstOrDefault(s => Property.PropertyKeyComparer.Equals(s.Id, key));
            return property != null;
        }

        #endregion
    }
}
