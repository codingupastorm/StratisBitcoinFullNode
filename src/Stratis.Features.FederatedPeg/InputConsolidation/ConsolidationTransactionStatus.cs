﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Features.FederatedPeg.InputConsolidation
{
    /// <summary>
    /// Progress made on this <see cref="ConsolidationTransaction"/>.
    /// </summary>
    public enum ConsolidationTransactionStatus
    {
        Partial = 'P',
        FullySigned = 'F',
        SeenInBlock = 'S'
    }
}
