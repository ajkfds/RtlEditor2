using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ajkControls;
using ajkControls.ColorLabel;

namespace pluginVerilog.Verilog.DataObjects.Nets
{
    public class Net : DataObject
    {
        public bool Signed = false;

        public NetTypeEnum NetType = NetTypeEnum.Wire;

        public List<DataObjects.Range> PackedDimensions { get; set; } = new List<DataObjects.Range>();
        public DataObjects.Range Range
        {
            get
            {
                if (PackedDimensions.Count < 1) return null;
                return PackedDimensions[0];
            }
        }

        // net_type::= supply0 | supply1 | tri     | triand  | trior | tri0 | tri1 | wire  | wand   | wor

        // SystemVerilog 2012
        // net_type::= supply0 | supply1 | tri | triand | trior | trireg| tri0 | tri1 | uwire| wire | wand | wor
        public enum NetTypeEnum
        {
            Supply0,
            Supply1,
            Tri,
            Triand,
            Trior,
            Trireg,
            Tri0,
            Tri1,
            Uwire,
            Wire,
            Wand,
            Wor
        }

        public override void AppendTypeLabel(ColorLabel label)
        {
            switch (NetType)
            {
                case NetTypeEnum.Supply0:
                    label.AppendText("supply0", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case NetTypeEnum.Supply1:
                    label.AppendText("supply1", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case NetTypeEnum.Tri:
                    label.AppendText("tri", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case NetTypeEnum.Triand:
                    label.AppendText("triand", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case NetTypeEnum.Trior:
                    label.AppendText("trior", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case NetTypeEnum.Trireg:
                    label.AppendText("trireg", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case NetTypeEnum.Tri0:
                    label.AppendText("tri0", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case NetTypeEnum.Tri1:
                    label.AppendText("tri1", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case NetTypeEnum.Uwire:
                    label.AppendText("uwire", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case NetTypeEnum.Wire:
                    label.AppendText("wire", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case NetTypeEnum.Wand:
                    label.AppendText("wand", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case NetTypeEnum.Wor:
                    label.AppendText("wor", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
            }

            label.AppendText(" ");
            if (Signed)
            {
                label.AppendText("signed ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
            }

            if (Range != null)
            {
                label.AppendLabel(Range.GetLabel());
                label.AppendText(" ");
            }
        }

        public override void AppendLabel(ColorLabel label)
        {
            AppendTypeLabel(label);
            if (Name == null) return;

            label.AppendText(Name, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Net));

            foreach (DataObjects.Range dimension in Dimensions)
            {
                label.AppendText(" ");
                label.AppendLabel(dimension.GetLabel());
            }

            if (Comment != "")
            {
                label.AppendText(" ");
                label.AppendText(Comment.Trim(new char[] { '\r', '\n', '\t', ' ' }), Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Comment));
            }

            label.AppendText("\r\n");
        }

        public static Net Create(NetTypeEnum netType, DataObjects.DataTypes.DataType dataType)
        {
            Net net = new Net();
            net.NetType = netType;
            if(dataType == null)
            {
                net.DataType = DataTypes.DataTypeEnum.Logic;
            }
            else
            {
                net.DataType = dataType.Type;
            }
            if (dataType is DataObjects.DataTypes.IntegerVectorType)
            {
                var integerVectorType = (DataObjects.DataTypes.IntegerVectorType)dataType;
                net.PackedDimensions = integerVectorType.PackedDimensions;
                net.Signed = integerVectorType.Signed;
            }
            else if (dataType is DataObjects.DataTypes.IntegerAtomType)
            {
                var integerAtomType = (DataObjects.DataTypes.IntegerAtomType)dataType;
                net.Signed = integerAtomType.Signed;
            }
            return net;
        }


        public static NetTypeEnum? parseNetType(WordScanner word, NameSpace nameSpace)
        {
            NetTypeEnum? ret;
            switch (word.Text)
            {
                case "supply0":
                    ret = NetTypeEnum.Supply0;
                    break;
                case "supply1":
                    ret = NetTypeEnum.Supply1;
                    break;
                case "tri":
                    ret = NetTypeEnum.Tri;
                    break;
                case "triand":
                    ret = NetTypeEnum.Triand;
                    break;
                case "trior":
                    ret = NetTypeEnum.Trior;
                    break;
                case "trireg":
                    ret = NetTypeEnum.Trireg;
                    break;
                case "tri0":
                    ret = NetTypeEnum.Tri0;
                    break;
                case "tri1":
                    ret = NetTypeEnum.Tri1;
                    break;
                case "uwire":
                    ret = NetTypeEnum.Uwire;
                    break;
                case "wire":
                    ret = NetTypeEnum.Wire;
                    break;
                case "wand":
                    ret = NetTypeEnum.Wand;
                    break;
                case "wor":
                    ret = NetTypeEnum.Wor;
                    break;
                default:
                    return null;
            }
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();
            return ret;
        }

        public static bool ParseDeclaration(WordScanner word, NameSpace nameSpace)
        {
            // ### Verilog 2001

            // net_declaration ::=    net_type                                          [signed]        [delay3] list_of_net_identifiers;
            //                      | net_type[drive_strength]                          [signed]        [delay3] list_of_net_decl_assignments;
            //                      | net_type                  [vectored | scalared]   [signed] range  [delay3] list_of_net_identifiers;
            //                      | net_type[drive_strength]  [vectored | scalared]   [signed] range  [delay3] list_of_net_decl_assignments;
            //                      | trireg[charge_strength][signed][delay3] list_of_net_identifiers;
            //                      | trireg[drive_strength][signed][delay3] list_of_net_decl_assignments;
            //                      | trireg[charge_strength][vectored | scalared][signed] range[delay3] list_of_net_identifiers;          
            //                      | trireg[drive_strength][vectored | scalared][signed] range[delay3] list_of_net_decl_assignments; 
            //
            //
            // list_of_net_decl_assignments ::= net_decl_assignment { , net_decl_assignment }
            // list_of_net_identifiers      ::= net_identifier [ dimension { dimension }]    { , net_identifier [ dimension { dimension }] }
            // net_decl_assignment          ::= net_identifier = expression
            // dimension                    ::= [ dimension_constant_expression : dimension_constant_expression ]
            // range                        ::= [ msb_constant_expression : lsb_constant_expression ] 

            // ### SystemVerilog 2012
            // net_declaration  ::=
            //        net_type              [drive_strength | charge_strength] [vectored | scalared] data_type_or_implicit [delay3] list_of_net_decl_assignments;
            //      | net_type_identifier   [delay_control] list_of_net_decl_assignments;
            //      | interconnect implicit_data_type[ # delay_value ] net_identifier { unpacked_dimension }  [ , net_identifier { unpacked_dimension }] ;
            //
            // data_type_or_implicit::= data_type | implicit_data_type
            // implicit_data_type ::= [ signing ] { packed_dimension } 

            // integer_vector_type::= bit | logic | reg

            // valid net datat type :
            //  a) A 4 - state integral type, including a packed array or packed structure.
            //  b) A fixed-size unpacked array or unpacked structure, where each element has a valid data type for a net.

            // implicit data type : logic

            //  The effect of this recursive definition is that a net is composed entirely of 4 - state bits and is treated
            //  accordingly. In addition to a signal value, each bit of a net shall have additional strength information.When
            //  bits of signals combine, the strength and value of the resulting signal shall be determined as described in 28.12.

            // A lexical restriction applies to the use of the reg keyword in a net or port declaration. A net type keyword
            // shall not be followed directly by the reg keyword. Thus, the following declarations are in error:

            // The reg keyword can be used in a net or port declaration if there are lexical elements between the net type
            // keyword and the reg keyword. 

            NetTypeEnum netType = NetTypeEnum.Wire;
            {
                NetTypeEnum? netTypeTemp = parseNetType(word, nameSpace);
                if (netTypeTemp == null)
                {
                    netType = NetTypeEnum.Wire;
                }
                else
                {
                    netType = (NetTypeEnum)netTypeTemp;
                }
            }

            // [drive_strength | charge_strength]
            DriveStrength driveStrength = DriveStrength.ParseCreate(word, nameSpace);

            // [vectored | scalared]


            if (word.Eof)
            {
                word.AddError("illegal net declaration");
                return true;
            }

            // [signed]
            bool signed = false;
            if (word.Text == "signed")
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
                signed = true;
            }

            if (word.Eof)
            {
                word.AddError("illegal net declaration");
                return true;
            }

            // [range]
            DataObjects.Range range = null;
            if (word.GetCharAt(0) == '[')
            {
                range = DataObjects.Range.ParseCreate(word, nameSpace);
                if (word.Eof || range == null)
                {
                    word.AddError("illegal net declaration");
                    return true;
                }
            }
            else
            {
//                DataTypes.DataType dataType = DataTypes.DataType.ParseCreate(word, nameSpace,DataTypes.DataTypeEnum.Logic);
            }



            if (!General.IsIdentifier(word.Text))
            {
                word.AddError("illegal net identifier");
                return true;
            }
            //[delay3]
            // TODO

            List<Net> nets = new List<Net>();
            while (!word.Eof)
            {
                Net net = new Net();
                nets.Add(net);
                net.Signed = signed;
                if(range != null)  net.PackedDimensions.Add(range);
                net.Name = word.Text;
                net.NetType = netType;

                net.DefinedReference = word.GetReference();
                if (word.Active)
                {
                    if (word.Prototype)
                    {
                        if (nameSpace.DataObjects.ContainsKey(net.Name))
                        {
                            word.RootPointer.AddError(net.DefinedReference, "duplicated net name");
                        }
                        else
                        {
                            nameSpace.DataObjects.Add(net.Name, net);
                        }
                        word.Color(CodeDrawStyle.ColorType.Net);
                    }
                    else
                    {
                        if (nameSpace.DataObjects.ContainsKey(net.Name) && nameSpace.DataObjects[net.Name] is Net)
                        {
                            net = nameSpace.DataObjects[net.Name] as Net;
                        }
                        word.Color(CodeDrawStyle.ColorType.Net);
                    }
                }

                word.MoveNext();

                if (word.Text == "=")
                {
                    word.MoveNext();
                    Expressions.Expression initalValue = Expressions.Expression.ParseCreate(word, nameSpace);
                    net.AssignedReferences.Add(net.DefinedReference);
                }
                else if (word.Text == "[")
                {
                    while (word.Text == "[")
                    {
                        DataObjects.Range dimension = DataObjects.Range.ParseCreate(word, nameSpace);
                        if (word.Active && word.Prototype)
                        {
                            net.Dimensions.Add(dimension);
                        }
                    }
                }

                if (word.GetCharAt(0) != ',') break;
                word.MoveNext(); // ,
            }

            if (word.Eof || word.GetCharAt(0) != ';')
            {
                word.AddError("; expected");
            }
            else
            {
                word.MoveNext();
                string comment = word.GetFollowedComment();
                foreach (Net net in nets)
                {
                    net.Comment = comment;
                }
            }

            return true;
        }
    }
}
