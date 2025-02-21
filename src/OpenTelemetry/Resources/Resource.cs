﻿// <copyright file="Resource.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
using System;
using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Utils;

namespace OpenTelemetry.Resources
{
    /// <summary>
    /// <see cref="Resource"/> represents a resource, which captures identifying information about the entities
    /// for which signals (stats or traces) are reported.
    /// </summary>
    public class Resource
    {
        // this implementation follows https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/sdk-resource.md

        /// <summary>
        /// Maximum length of the resource type name.
        /// </summary>
        private const int MaxResourceTypeNameLength = 255;

        /// <summary>
        /// Creates a new <see cref="Resource"/>.
        /// </summary>
        /// <param name="attributes">An <see cref="IDictionary{String, String}"/> of attributes that describe the resource.</param>
        public Resource(IEnumerable<KeyValuePair<string, string>> attributes)
        {
            ValidateAttributes(attributes);
            this.Attributes = attributes;
        }

        /// <summary>
        /// Gets an empty Resource.
        /// </summary>
        public static Resource Empty { get; } = new Resource(Enumerable.Empty<KeyValuePair<string, string>>());

        /// <summary>
        /// Gets the collection of key-value pairs describing the resource.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Attributes { get; }

        /// <summary>
        /// Returns a new, merged <see cref="Resource"/> by merging the current <see cref="Resource"/> with the.
        /// <code>other</code> <see cref="Resource"/>. In case of a collision the current <see cref="Resource"/> takes precedence.
        /// </summary>
        /// <param name="other">The <see cref="Resource"/> that will be merged with. <code>this</code>.</param>
        /// <returns><see cref="Resource"/>.</returns>
        public Resource Merge(Resource other)
        {
            var newAttributes = new Dictionary<string, string>();

            foreach (var attribute in this.Attributes)
            {
                if (!newAttributes.TryGetValue(attribute.Key, out var value) || string.IsNullOrEmpty(value))
                {
                    newAttributes[attribute.Key] = attribute.Value;
                }
            }

            if (other != null)
            {
                foreach (var attribute in other.Attributes)
                {
                    if (!newAttributes.TryGetValue(attribute.Key, out var value) || string.IsNullOrEmpty(value))
                    {
                        newAttributes[attribute.Key] = attribute.Value;
                    }
                }
            }

            return new Resource(newAttributes);
        }

        private static void ValidateAttributes(IEnumerable<KeyValuePair<string, string>> attributes)
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            foreach (var attribute in attributes)
            {
                if (!IsValidAndNotEmpty(attribute.Key))
                {
                    throw new ArgumentException($"Attribute key should be a string with a length greater than 0 and not exceeding {MaxResourceTypeNameLength} characters.");
                }

                if (!IsValid(attribute.Value))
                {
                    throw new ArgumentException($"Attribute value should be a string with a length not exceeding {MaxResourceTypeNameLength} characters.");
                }
            }
        }

        private static bool IsValidAndNotEmpty(string name)
        {
            return !string.IsNullOrEmpty(name) && IsValid(name);
        }

        private static bool IsValid(string name)
        {
            return name != null && name.Length <= MaxResourceTypeNameLength && StringUtil.IsPrintableString(name);
        }
    }
}
