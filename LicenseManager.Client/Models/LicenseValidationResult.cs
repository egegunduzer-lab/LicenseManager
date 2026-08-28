using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LicenseManager.Client.Models;

public class LicenseValidationResult
{
    public bool IsValid { get; set; }

    public string Message { get; set; } = string.Empty;
}