using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorPickerWPF.Code
{
    [Flags]
    public enum ColorPickerDialogOptions
    {
        None,
        SimpleView,
        LoadCustomPalette
    }
}
