﻿using System.ComponentModel.DataAnnotations;

namespace Stratis.Feature.PoA.Tokenless.Controllers.Models
{
    public sealed class BuildCreateContractTransactionModel
    {
        [Required]
        public byte[] ContractCode { get; set; }

        public string[] Parameters { get; set; }
    }
}