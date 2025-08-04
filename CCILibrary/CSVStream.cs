﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CCILibrary;

namespace CSVStream
{

    public class CSVInputStream
    {
        public Variables CSVVariables { get; private set; }
        int _numberOfRecords;
        public int NumberOfRecords
        {
            get
            {
                return _numberOfRecords;
            }
        }
        StreamReader reader;
        static Regex nameParse = new Regex(@"^(?'name'[A-Za-z][A-Za-z_0-9]*(\([0-9]+\))?[A-Za-z_0-9]*)(?'string'\$)?$"); //for validation of SYSTAT variable names
        static Regex valueParse = new Regex(@"(^|,)((?<d>[^,""]*?)|(\""(?<d>([^\""]|\""\"")*?)\""))(?=(,|$))"); //for comma separated values, including quoted values

        public CSVInputStream(string path)
        {
            try
            {
                reader = new StreamReader(path, Encoding.ASCII);
                string line = reader.ReadLine(); //get first line which contains variable names
                string[] names = Regex.Split(line, @"\s*,\s*");
                CSVVariables = new Variables();
                foreach (string name in names)
                {
                    Match m = nameParse.Match(name);
                    if (m.Success)
                    {
                        Variable v = new Variable(m.Groups["name"].Value,
                            m.Groups["string"].Length > 0 ? SVarType.String : SVarType.Number);
                        CSVVariables.Add(v);
                    }
                    else
                        throw new Exception("CSVInputStream: invalid variable name: " + name);
                }
                _numberOfRecords = 0;
                while (reader.ReadLine() != null) _numberOfRecords++;
                reader.Close();
                reader = new StreamReader(path, Encoding.ASCII);
                reader.ReadLine(); //skip header
            }
            catch(Exception e)
            {
                throw new Exception("CSVInputStream: Error creating from: " + path + "; " + e.Message);
            }
        }

        public void Read()
        {
            string line = reader.ReadLine();
            MatchCollection values = valueParse.Matches(line);
            int i = 0;
            foreach (Match value in values)
            {
                string s = value.Groups["d"].Value.Replace("\"\"", "\"").Trim(); //replace doubled quotes with single quotes
                Variable v = CSVVariables[i++];
                if (v.Type == SVarType.String)
                {
                    if (s == "")
                        v.Value = Variable.MissingString;
                    else
                        v.Value = s;
                }
                else
                    try
                    {
                        if (s == "" || s == ".")
                            v.Value = Variable.MissingNumber;
                        else
                            v.Value = Convert.ToDouble(s);
                    }
                    catch
                    {
                        throw new Exception("CSVInputStream: invalid value for variable " + v.Name + ": " + s);
                    }
            }
        }

        public void Close()
        {
            reader.Close();
        }
    }

    public class Variables : ObservableCollection<Variable> { }

    public class Variable: INotifyPropertyChanged
    {
        internal const double MissingNumber = double.NaN;
        internal const string MissingString = "";

        string _OriginalName; //this property doesn't change if type changes
        public string OriginalName
        {
            get { return _OriginalName; }
        }

        string _Name; //this property changes depending on the current type
        public string Name
        {
            get { return _Name + (IsNum ? "" : "$"); }
        }

        private int _MaxLength;
        public int MaxLength
        {
            set
            {
                if (value == _MaxLength) return;
                _MaxLength = value;
                Notify("MaxLength");
            }
            get { return _MaxLength; }
        }

        bool _lengthError;
        public bool LengthError
        {
            set
            {
                _lengthError = value;
                Notify("LengthError");
            }
            get { return _lengthError; }
        }

        public string BaseName //this property doesn't change and doesn't include any terminating $
        {
            get { return _Name; }
        }
        
        SVarType _Type;
        public SVarType Type
        {
            get { return _Type; }
            set
            {
                if (_Type == value) return;
                _Type = value;
                Notify("Name");
                Notify("IsStr");
            }
        }
        public object Value { get; internal set; } //read only, input only

        public bool IsSel { get; set; }

        public bool IsNum
        {
            get
            {
                return _Type == SVarType.Number;
            }
        }

        public bool IsStr
        {
            get
            {
                return _Type == SVarType.String;
            }
        }

        internal Variable(string name, SVarType type)
        {
            _Name = name;
            _Type = type;
            _OriginalName = Name;
            _MaxLength = type == SVarType.Number ? 8 : 16;
        }

        //Items used to display combobox selections
        public static SVarType[] _comboStringOnly = { SVarType.String };
        public SVarType[] comboStringOnly
        {
            get { return _comboStringOnly; }
        }
        public static SVarType[] _combo = { SVarType.Number, SVarType.String};
        public SVarType[] combo
        {
            get { return _combo; }
        }

        public override string ToString(){
            return (IsSel ? "*" : "") + Name + "=" + (Value == null ? "null" : Value.ToString()) +
                "(" + MaxLength.ToString("0") + ")";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string p)
        {
            if (this.PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(p));
        }
    }
}
