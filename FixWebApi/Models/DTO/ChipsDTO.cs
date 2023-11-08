using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.BetFairClasses
{
    public class ChipsDTO
    {
        public List<Plus> PlusObj { get; set; }
        public List<Minus> MinusObj { get; set; }
        public double PlusSum { get; set; }
        public double MinusSum { get; set; }
    }
    public class Plus
    {
        public int id { get; set; }
        public string UserId { get; set; }
        public string Role { get; set; }
        public double Chips { get; set; }
    }
    public class Minus
    {
        public int id { get; set; }
        public string UserId { get; set; }
        public string Role { get; set; }
        public double Chips { get; set; }
    }
   
}