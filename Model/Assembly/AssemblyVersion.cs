﻿using System;
using GalaSoft.MvvmLight;

namespace LeagueSharp.Loader.Model.Assembly
{
    internal class AssemblyVersion : ObservableObject
    {
        private string _color;
        private DateTimeOffset _date;
        private int _id;
        private string _message;

        public AssemblyVersion()
        {
            Color = "Green";
        }

        public string Color
        {
            get { return _color; }
            set { Set(() => Color, ref _color, value); }
        }

        public DateTimeOffset Date
        {
            get { return _date; }
            set { Set(() => Date, ref _date, value); }
        }

        public int Id
        {
            get { return _id; }
            set { Set(() => Id, ref _id, value); }
        }

        public string Message
        {
            get { return _message; }
            set { Set(() => Message, ref _message, value); }
        }

        public override string ToString()
        {
            return string.Format("{0} - {1} >> {2}", Id, Date, Message);
        }
    }
}