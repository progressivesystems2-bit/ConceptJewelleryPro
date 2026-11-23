using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptJewelleryPro.Domain.Entities;

public class CardSetupItem
{
    public string CardBank { get; set; } = string.Empty;
    public decimal CardComm { get; set; }
    public decimal Sbi { get; set; }
    public int Order { get; set; }
}
