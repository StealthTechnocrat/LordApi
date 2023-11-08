using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FixWebApi.Models.DTO
{
    public class BookDTOList
    {
        public List<BookDTO> bookDTOs { get; set; }
    }
    public class BookDTO
    {
        public double ProfitLoss { get; set; }
        public double totalSum { get; set; }
        public int RunnerId { get; set; }
        public int UserId { get; set; }
        public string Role { get; set; }
        public string UserName { get; set; }
    }
}