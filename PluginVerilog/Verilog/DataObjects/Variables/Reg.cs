using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ajkControls;
using ajkControls.ColorLabel;
using pluginVerilog.Verilog.DataObjects.DataTypes;

namespace pluginVerilog.Verilog.DataObjects.Variables
{
    public class Reg : IntegerVectorValueVariable
    {
        protected Reg() { }

        public static new Reg Create(DataType dataType)
        {
            System.Diagnostics.Debug.Assert(dataType.Type == DataTypeEnum.Reg);
            DataTypes.IntegerVectorType dType = dataType as DataTypes.IntegerVectorType;

            Reg val = new Reg();
            val.PackedDimensions = dType.PackedDimensions;
            val.DataType = dType.Type;
            return val;
        }

        public override Variable Clone()
        {
            Reg val = new Reg();
            val.DataType = DataType;
            val.PackedDimensions = PackedDimensions;
            val.Signed = Signed;
            return val;
        }

        //public static new Reg ParseCreateType(WordScanner word,NameSpace nameSpace)
        //{
        //    if (word.Text != "reg") System.Diagnostics.Debugger.Break();
        //    word.Color(CodeDrawStyle.ColorType.Keyword);
        //    word.MoveNext(); // reg


        //    Reg type = new Reg();
        //    type.Signed = false;

        //    if (word.Eof)
        //    {
        //        word.AddError("illegal reg declaration");
        //        return null;
        //    }
        //    if (word.Text == "signed")
        //    {
        //        word.Color(CodeDrawStyle.ColorType.Keyword);
        //        word.MoveNext();
        //        type.Signed = true;
        //    }
        //    if (word.Eof)
        //    {
        //        word.AddError("illegal reg declaration");
        //        return null;
        //    }

        //    type.Range = null;
        //    if (word.GetCharAt(0) == '[')
        //    {
        //        type.Range = Range.ParseCreate(word, nameSpace);
        //        if (word.Eof || type.Range == null)
        //        {
        //            word.AddError("illegal reg declaration");
        //            return null;
        //        }
        //    }
        //    return type;
        //}

        //public static Reg CreateFromType(string name,Reg type)
        //{
        //    Reg reg = new Reg();
        //    reg.Signed = type.Signed;
        //    reg.Range = type.Range;
        //    reg.Name = name;
        //    return reg;
        //}

        //public static void ParseCreateFromDeclaration(WordScanner word, NameSpace nameSpace)
        //{
        //    // reg_declaration::= reg [signed] [range] list_of_variable_identifiers;

        //    Reg type = Reg.ParseCreateType(word, nameSpace);
        //    if (type == null)
        //    {
        //        word.SkipToKeyword(";");
        //        if (word.Text == ";") word.MoveNext();
        //        return;
        //    }

        //    ParseCreateFromDeclaration(word, nameSpace, type);
        //}

        //public static void ParseCreateFromDeclaration(WordScanner word, NameSpace nameSpace, Reg type)
        //{
        //    if (!(type is Reg)) System.Diagnostics.Debugger.Break();

        //    List<Reg> regs = new List<Reg>();
        //    while (!word.Eof)
        //    {
        //        if (!General.IsIdentifier(word.Text))
        //        {
        //            word.AddError("illegal reg identifier");
        //            return;
        //        }

        //        Reg reg = Reg.CreateFromType(word.Text, type);
        //        WordReference nameRef = word.GetReference();
        //        reg.DefinedReference = word.GetReference();
        //        regs.Add(reg);

        //        // register valiable
        //        if (!word.Active)
        //        {
        //            // skip
        //        }
        //        else if (word.Prototype)
        //        {
        //            if (nameSpace.Variables.ContainsKey(reg.Name))
        //            {
        //                if (nameSpace.Variables[reg.Name] is Net)
        //                {
        //                    nameSpace.Variables.Remove(reg.Name);
        //                    nameSpace.Variables.Add(reg.Name, reg);
        //                }
        //                else
        //                {
        //                    //                            nameRef.AddError("duplicated reg name");
        //                }
        //            }
        //            else
        //            {
        //                nameSpace.Variables.Add(reg.Name, reg);
        //            }
        //            word.Color(CodeDrawStyle.ColorType.Register);
        //        }
        //        else
        //        {
        //            if (nameSpace.Variables.ContainsKey(reg.Name) && nameSpace.Variables[reg.Name] is Reg)
        //            {
        //                reg = nameSpace.Variables[reg.Name] as Reg;
        //            }
        //            word.Color(CodeDrawStyle.ColorType.Register);
        //        }

        //        word.MoveNext();

        //        if (word.Text == "=")
        //        {
        //            word.MoveNext();
        //            Expressions.Expression initalValue = Expressions.Expression.ParseCreate(word, nameSpace);
        //            reg.AssignedReferences.Add(reg.DefinedReference);
        //        }
        //        else if (word.Text == "[")
        //        {
        //            while (word.Text == "[")
        //            {
        //                Dimension dimension = Dimension.ParseCreate(word, nameSpace);
        //                if (word.Active && word.Prototype)
        //                {
        //                    reg.dimensions.Add(dimension);
        //                }
        //            }
        //        }

        //        if (word.GetCharAt(0) != ',') break;
        //        word.MoveNext();
        //    }

        //    if (word.Eof || word.GetCharAt(0) != ';')
        //    {
        //        word.AddError("; expected");
        //    }
        //    else
        //    {
        //        word.MoveNext();
        //        string comment = word.GetFollowedComment();
        //        foreach (Reg reg in regs)
        //        {
        //            reg.Comment = comment;
        //        }
        //    }

        //    return;
        //}

    }
}
