using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OtzariaIndexer
{
    public class Token
    {
        public int DocumentId { get; set; }
        public string Text { get; set; }
        public int Position { get; set; }
        public int StartIndex { get; set; }
    }
}
