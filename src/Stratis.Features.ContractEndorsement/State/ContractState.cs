using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Features.ContractEndorsement.State
{
    /// <summary>
    /// A contract's state.
    /// </summary>
    public class ContractState
    {
        /// <summary>
        /// 32 byte hash of the code deployed at this contract.
        /// Can be used to lookup the actual code in the code table
        /// </summary>
        public byte[] CodeHash { get; set; }


        /// <summary>
        /// Name of the type to instantiate within the assembly.
        /// </summary>
        public string TypeName { get; set; }

        public ContractState() { }

        #region Serialization

        public ContractState(byte[] bytes) : this()
        {
            RLPCollection list = RLP.Decode(bytes);
            RLPCollection innerList = (RLPCollection)list[0];
            this.CodeHash = innerList[0].RLPData;
            this.TypeName = innerList[1].RLPData == null ? null : Encoding.UTF8.GetString(innerList[1].RLPData);
        }

        public byte[] ToBytes()
        {
            return RLP.EncodeList(
                RLP.EncodeElement(this.CodeHash ?? new byte[0]),
                RLP.EncodeElement(this.TypeName == null ? new byte[0] : Encoding.UTF8.GetBytes(this.TypeName))
            );
        }

        #endregion
    }
}
