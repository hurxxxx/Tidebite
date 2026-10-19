#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Tidebite.Core
{
    public static class AdmissionTickets
    {
        public static bool ValidRoom(string value)
        {
            if(string.IsNullOrEmpty(value)||value.Length>32)return false;
            foreach(char c in value)if(!(c>='a'&&c<='z'||c>='0'&&c<='9'||c=='-'||c=='_'))return false;
            return true;
        }
        public static string Create(string key,string room,string nonce,long expiry,int protocol)
        {
            if(key==null||Encoding.UTF8.GetByteCount(key)<32||!ValidRoom(room)||!Guid.TryParseExact(nonce,"N",out _))throw new ArgumentException("Invalid admission configuration.");
            string payload=room+"|"+nonce+"|"+expiry.ToString(CultureInfo.InvariantCulture)+"|"+protocol.ToString(CultureInfo.InvariantCulture);
            byte[] data=Encoding.UTF8.GetBytes(payload);
            using(var hmac=new HMACSHA256(Encoding.UTF8.GetBytes(key)))return Encode(data)+"."+Encode(hmac.ComputeHash(data));
        }
        public static bool Verify(string token,string key,string room,int protocol,long now,out string nonce,out long expiry)
        {
            nonce=null;expiry=0;
            if(string.IsNullOrEmpty(token)||token.Length>512||key==null||Encoding.UTF8.GetByteCount(key)<32)return false;
            try
            {
                string[] parts=token.Split('.');if(parts.Length!=2)return false;
                byte[] data=Decode(parts[0]),signature=Decode(parts[1]);
                using(var hmac=new HMACSHA256(Encoding.UTF8.GetBytes(key)))if(!Equal(signature,hmac.ComputeHash(data)))return false;
                string[] fields=Encoding.UTF8.GetString(data).Split('|');
                if(fields.Length!=4||fields[0]!=room||!Guid.TryParseExact(fields[1],"N",out _)||
                    !long.TryParse(fields[2],NumberStyles.None,CultureInfo.InvariantCulture,out expiry)||
                    !int.TryParse(fields[3],NumberStyles.None,CultureInfo.InvariantCulture,out int version)||version!=protocol||expiry<=now||expiry>now+60)return false;
                nonce=fields[1];return true;
            }
            catch(FormatException){return false;}
        }
        public static bool Equal(byte[] a,byte[] b)
        {if(a.Length!=b.Length)return false;int different=0;for(int i=0;i<a.Length;i++)different|=a[i]^b[i];return different==0;}
        private static string Encode(byte[] value)=>Convert.ToBase64String(value).TrimEnd('=').Replace('+','-').Replace('/','_');
        private static byte[] Decode(string value)
        {value=value.Replace('-','+').Replace('_','/');return Convert.FromBase64String(value.PadRight((value.Length+3)/4*4,'='));}
    }
    public sealed class AdmissionGate
    {
        private readonly string key,room;
        private readonly Dictionary<string,long> used=new Dictionary<string,long>();
        private readonly List<string> expired=new List<string>();
        public AdmissionGate(string room,string key)
        {
            if(!AdmissionTickets.ValidRoom(room)||key==null||Encoding.UTF8.GetByteCount(key)<32)throw new ArgumentException("Room admission requires a room ID and a key of at least 32 bytes.");
            this.room=room;this.key=key;
        }
        public bool Admit(string token,int protocol,long now,out string nonce)
        {
            nonce=null;
            if(!AdmissionTickets.Verify(token,key,room,protocol,now,out var id,out var expiry))return false;
            expired.Clear();foreach(var entry in used)if(entry.Value<=now)expired.Add(entry.Key);
            foreach(string idExpired in expired)used.Remove(idExpired);
            if(used.ContainsKey(id))return false;
            used.Add(id,expiry);nonce=id;return true;
        }
    }
}
