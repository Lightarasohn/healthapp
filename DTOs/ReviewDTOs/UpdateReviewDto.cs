using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.DTOs.ReviewDTOs
{
    public class UpdateReviewDto
    {
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}