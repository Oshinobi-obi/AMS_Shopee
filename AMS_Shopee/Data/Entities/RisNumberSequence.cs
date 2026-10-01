using System;
using System.Collections.Generic;

namespace AMS_Shopee.Data.Entities;

public partial class RisNumberSequence
{
    public ushort FiscalYear { get; set; }

    public byte SeqMonth { get; set; }

    public uint LastSeq { get; set; }
}
