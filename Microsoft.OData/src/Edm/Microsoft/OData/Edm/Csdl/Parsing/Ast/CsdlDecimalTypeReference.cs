//   OData .NET Libraries
//   Copyright (c) Microsoft Corporation
//   All rights reserved. 

//   Licensed under the Apache License, Version 2.0 (the ""License""); you may not use this file except in compliance with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0 

//   THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR NON-INFRINGEMENT. 

//   See the Apache Version 2.0 License for specific language governing permissions and limitations under the License.

namespace Microsoft.OData.Edm.Csdl.Parsing.Ast
{
    /// <summary>
    /// Represents a reference to a CSDL decimal type.
    /// </summary>
    internal class CsdlDecimalTypeReference : CsdlPrimitiveTypeReference
    {
        private readonly int?precision;
        private readonly int?scale;

        public CsdlDecimalTypeReference(int? precision, int? scale, string typeName, bool isNullable, CsdlLocation location)
            : base(EdmPrimitiveTypeKind.Decimal, typeName, isNullable, location)
        {
            this.precision = precision;
            this.scale = scale;
        }

        public int? Precision
        {
            get { return this.precision; }
        }

        public int? Scale
        {
            get { return this.scale; }
        }
    }
}
