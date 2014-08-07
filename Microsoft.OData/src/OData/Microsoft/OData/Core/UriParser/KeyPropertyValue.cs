//   OData .NET Libraries
//   Copyright (c) Microsoft Corporation
//   All rights reserved. 

//   Licensed under the Apache License, Version 2.0 (the ""License""); you may not use this file except in compliance with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0 

//   THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR NON-INFRINGEMENT. 

//   See the Apache Version 2.0 License for specific language governing permissions and limitations under the License.

namespace Microsoft.OData.Core.UriParser
{
    #region Namespaces
    using Microsoft.OData.Edm;
    using Microsoft.OData.Core.UriParser.Semantic;
    #endregion Namespaces

    /// <summary>
    /// Class representing a single key property value in a key lookup.
    /// </summary>
    public sealed class KeyPropertyValue
    {
        /// <summary>
        /// Gets or sets the key property.
        /// </summary>
        public IEdmProperty KeyProperty
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the value of the key property.
        /// </summary>
        public SingleValueNode KeyValue
        {
            get;
            set;
        }
    }
}
