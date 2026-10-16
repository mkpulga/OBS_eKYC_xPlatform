using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OBS.Maui.Controls.Model
{
   public sealed record CollectionModel(
       string No, 
       string referencenumber, 
       string cid, 
       string sbrid, 
       string lastname, 
       string firstname, 
       string middlename, 
       string dateofbirth, 
       string barangay, 
       string datecreated, 
       string citizenid, string streetname, string unitnumber, string addressline1, string addressline2, int id, string timestamp, string remarks, string status, string fullname, string createdby, string suffix, string msisdn, string issuedby, int code, string message);
   

}
