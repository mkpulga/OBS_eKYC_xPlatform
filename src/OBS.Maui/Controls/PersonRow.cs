using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OBS.Maui.Controls
{
    public sealed record PersonRow(
    int Id,
    string Name,
    DateTime BirthDate);

}
