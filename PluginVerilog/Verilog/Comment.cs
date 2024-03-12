using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog
{
    public class Comment
    {

        public void ParseComment(string fullComment, out string followedComment,out List<string> tags)
        {
            tags = null;
            followedComment = fullComment;
        }
    }
}
