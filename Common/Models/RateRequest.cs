using System;
using System.Runtime.Serialization;

namespace Common.Models
{
    [DataContract]
    public class RateRequest
    {
        [DataMember]
        public string HotelId { get; set; }
        [DataMember]
        public string HotelName { get; set; }
        [DataMember]
        public DateTime CheckInDate { get; set; }
        [DataMember]
        public DateTime CheckOutDate { get; set; }
        [DataMember]
        public string City { get; set; }
        [DataMember]
        public string Country { get; set; }
    }
}
