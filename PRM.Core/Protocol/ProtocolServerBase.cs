using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorPickerWPF.Code;
using Newtonsoft.Json;
using Shawn.Ulits;
using Color = System.Windows.Media.Color;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolServerBase : NotifyPropertyChangedBase
    {
        protected ProtocolServerBase(string serverType, string classVersion, string protocolFullName)
        {
            ServerType = serverType;
            ClassVersion = classVersion;
            ProtocolFullName = protocolFullName;
        }


        private uint _id = 0;
        [JsonIgnore]
        public uint Id
        {
            get => _id;
            set => SetAndNotifyIfChanged(nameof(Id), ref _id, value);
        }



        private string _serverType = "";
        public string ServerType
        {
            get => _serverType;
            protected set => SetAndNotifyIfChanged(nameof(ServerType), ref _serverType, value);
        }

        private string _classVersion = "";
        public string ClassVersion
        {
            get => _classVersion;
            protected set => SetAndNotifyIfChanged(nameof(ClassVersion), ref _classVersion, value);
        }

        private string _protocolFullName = "";
        public string ProtocolFullName
        {
            get => _protocolFullName;
            protected set => SetAndNotifyIfChanged(nameof(ProtocolFullName), ref _protocolFullName, value);
        }

        private string _dispName = "";
        public string DispName
        {
            get => _dispName;
            set
            {
                SetAndNotifyIfChanged(nameof(DispName), ref _dispName, value);
            }
        }

        public string SubTitle => GetSubTitle();

        private string _groupName = "";
        public string GroupName
        {
            get => _groupName;
            set => SetAndNotifyIfChanged(nameof(GroupName), ref _groupName, value);
        }

        private string _iconBase64 = "";
        public string IconBase64
        {
            get => _iconBase64;
            set
            {
                try
                {
                    var bm = NetImageProcessHelper.BitmapFromBytes(Convert.FromBase64String(value));
                    var icon = bm.ToIcon();
                    IconImg = bm.ToBitmapSource();
                    Icon = icon;
                    SetAndNotifyIfChanged(nameof(IconBase64), ref _iconBase64, value);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }


        private Icon _icon;
        [JsonIgnore]
        public Icon Icon
        {
            get => _icon;
            private set => SetAndNotifyIfChanged(nameof(Icon), ref _icon, value);
        }


        private BitmapSource _iconImg;
        [JsonIgnore]
        public BitmapSource IconImg
        {
            get => _iconImg;
            set
            {
                _iconBase64 = Convert.ToBase64String(value.ToBytes());
                Icon = value.ToIcon();
                SetAndNotifyIfChanged(nameof(IconImg), ref _iconImg, value);
            }
        }


        private DateTime _lassConnTime = DateTime.Now;

        public DateTime LassConnTime
        {
            get => _lassConnTime;
            set => SetAndNotifyIfChanged(nameof(LassConnTime), ref _lassConnTime, value);
        }


        private string _markColorHex = "#FFFFFF";
        public string MarkColorHex
        {
            get => _markColorHex;
            set
            {
                try
                {
                    var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
                    SetAndNotifyIfChanged(nameof(MarkColorHex), ref _markColorHex, value);
                    SetAndNotifyIfChanged(nameof(MarkColor), ref _markColor, color);
                }
                catch (Exception)
                {
                }
            }
        }

        private Color _markColor = Colors.White;
        [JsonIgnore]
        public Color MarkColor
        {
            get => _markColor;
            set
            {
                SetAndNotifyIfChanged(nameof(MarkColorHex), ref _markColorHex, value.ToHexString());
                SetAndNotifyIfChanged(nameof(MarkColor), ref _markColor, value);
            }
        }


        public abstract void Conn();

        /// <summary>
        /// copy all value type fields
        /// </summary>
        public bool Update(ProtocolServerBase org)
        {
            var myType = this.GetType();
            var yourType = org.GetType();
            if (myType == yourType)
            {
                ProtocolServerBase copyObject = this;
                while (yourType != null)
                {
                    var fields = myType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    foreach (var fi in fields)
                    {
                        fi.SetValue(this, fi.GetValue(org));
                    }
                    var properties = myType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    foreach (var property in properties)
                    {
                        if (property.SetMethod != null)
                        {
                            // update properties without notify
                            property.SetValue(this, property.GetValue(org));
                            // then raise notify
                            base.RaisePropertyChanged(property.Name);
                        }
                    }
                    // update base class
                    yourType = yourType.BaseType;
                }
            }
            return false;
        }




        public abstract string ToJsonString();
        public abstract ProtocolServerBase CreateFromJsonString(string jsonString);





        public abstract string GetSubTitle();



        



        #region Static

        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);




        public const string Base64Icon1 =
            "iVBORw0KGgoAAAANSUhEUgAAADUAAAAwCAYAAACxKzLDAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAS4SURBVGhDzVlNiB" +
            "xFGN2j4B8KkohRZ7o7ieTgQSVRBBECigiCohgEQdSDRo0JJmaqasQBT4JK8KAHDQqKyh5ijLooCHvQ+Lv+zPT07kIOS4ISCCJGxSAI" +
            "+r6eV9O1M52d7u2e6X3w2J366r3+qqu6qqt6qkx0rmhcFPrm3sgzr3d9/S241PX0X/j7W9c3c/g7jd8vRYG5h5K1i/kr9aWRp5+NfP" +
            "0LEv8vCyPfdGNNoLbQZu2g66nd6Y1Rs+A7iL0AHkAPHYkb4uu/U+o2aVct5A4jwfcHEjwaevrJH2uNGqsNIartW9/xVKMb6IUB7Qyr" +
            "VINeg0zXSegTPEt3MpwJS7XWOaFnHoH2jPXBTfqd4cliuEHmeYZWBTxX2+FxMvFTswxNDgNDbprFhYHh+I31leHJ4vFDJgWnQYdZXB" +
            "rg+avjvwND+nKGxoP2ur3n4kJLvOA/7ZraxlBpWAjUDfR3eaIbmA/HsrZ1fP2AvZCsLywuHeLtNGiQX2J2fZBViyP01cc0/jMMmj6L" +
            "S4csB2HQuBmT0S55Mwl93XYaFRPP9WFMKDdSsnrA7GcxxEVeZdHEEPmNAA15Atc/ZhvWy0XtZZX8mPeaG/tmgX6MxcsgYx7xmX69fJ" +
            "zBsLuPVmdFVGutx0192dWu6lHo1NRVGMdvWZO0RXbEc5CHmZYIeWtxdZknrfnAXI9k+42x7PiNa1klhjy4g3UKMTA7aT0Sjm4p2tC6" +
            "mMXpwAr/nCMQYiyrZtobNWLTA3WLMvOCjufsNqvDzT3I4mGg4oF+RV+H6JnHoy2t8xgegq1bJmmdCWjMK1aHm34HixOgh950zD/PMn" +
            "U79UsjrTOhXW9uhgab0Fj7BYt7cIcceuujaMOelccoYTVlktaZ4U5Unbq5KS7klG1bezQuzAhrViZpnRltTGqJnhvOZc+Rp+6KCzMi" +
            "MSuPtM6F/qbTM59i6tbXoCH/SgEW0LdZJzPcZMoirXMBk9qhnl79IC18ODHM/z6VaBPmWeXT9Azlgry+UX8CeyTzojUL63od62SG1S" +
            "6nmpWGjSLqpq5xtM4F+on+DJKSU594TTrFeC7YRMokrXMButdEi/nhuPz4Izbz9NeM50KsHeLkewq6D3p6Mydj8ZT8kMMUxnPBJuJS" +
            "EmZ4JNL0DGXGnLf/QuiOixY9dUim8+9pdpp1coHaUknrzMDs/ajV4lXpbuyD9MGkIP85gNWWSVpnguyUHe13cSGGynZbGB8L5zzbdg" +
            "z7nNTwk22Qq4s2PpPsq1DwRj/QO6jcwdBIWN1yxufoGTmsp/VZ8dMmc5nswF2N7OsYTpCsyJbmJJ63r/D/u3Lnw0A9BKNbpSePBa0L" +
            "KBvb8Fvc/PT58jVlwVObpEfkMAY5PoW4zJi9GZtccWTIMRiEi65gLTPy1Xvznr6d6a+MqG62hoFWEH42aFQ5PX0EI2ZP6JnrmO7qIK" +
            "9Pi3V9NYbALRiG98vRlHS5MPXCBRn7BmanzMbxGWA85NUlTGf8SEuqKGldHdKSKkpaV4e0pIqS1tUhLamipHV1SEuqKGldHdKSKkpa" +
            "V4e0pIqS1tUhLamipHV1SEuqKGldHZBEZR8IxoYqP+WMFSW+A1bfSy4m8Xk0G6am/gdlIwp9EFIn9QAAAABJRU5ErkJggg==";

        public const string Base64Icon2 = "iVBORw0KGgoAAAANSUhEUgAAADUAAAAwCAYAAACxKzLDAAAAAXNSR0IArs4c6QAAAARnQU1B" +
                                          "AACxjwv8YQUAAAS7SURBVGhDzVlLqBxFFJ2loL443T15T4zanzGRLFyo+EEQIaCIICiKQRBE" +
                                          "XWj8xGDU6eonGXAlqIgLs9CgoKi4iDF+UBCy0PhNBPGBQhYhQQkEEX8YBCGe23NquudNvZnq" +
                                          "6Z7pOXB4r+vWOXWru7o+PY0qse6CTtOJkju8MHnFDdU34FHwb/A3N0wOeZF6xwvV806Q3E7J" +
                                          "/MK7UJ3rhGqXG6lf0IHTdkxWROO04820mR84YfyosTNBfACxN51IPYvrF8D90hH8/adfJ6u7" +
                                          "TLt6IXcYCb27KsGDuPvbz/E7PqsNoeU/vuSGcQd1f8xrMSw/YpV60OtQetd7SUXqYzdKbmHY" +
                                          "Cr7fPQPv3v3Qn+r7hOp3hmeLoQ6FyTMMTQR4bIHHib4fhi1DswMa7g85mc1YXBrw+1r7yvBk" +
                                          "8fSRTgq64UDtY3FlgOev2r8Zqa1YHs5naDpYXNx5JhqTdec0JoN/XT++kqHKgKF9te5Ujscx" +
                                          "PN+fytqGyeBu3ZCsLyyuHOl6N9ipjIH6AvF7WLU8MPQ+pPlf69vLEYsrhywHnt+5DrPpI7Iz" +
                                          "wVT//UDHhJHa14ziayiZHDD7maa7WTQztKJOGx15GG0fYQ4pvSDeySrF0QqXL+qbBepBFg9A" +
                                          "xrwsoPlGbSk68E5arYmW311C+y/mtRO9CpgQLob49b6RYZEd+R4UoO0Sgfa2D2htJy3HT66C" +
                                          "IOsM6UWdy1glhby4q+uUY7KN1mOR0x1d2NB1WGyGGyRP5wTCI7LpNO2o5e6uqluKRRZ0rGU3" +
                                          "5rR7WDwMBGVHrSv+4Aadh1qbu2cxPIRc3cpIayug/ktah4njZhZnwBN6rV8hVJ/ZTN26fpWk" +
                                          "tRW8YHkTNDiEpjl/zuIe8kMO78kHCxt2jB6jhNZUSVpbIz9RtYLk2rSQU3baW/BgWmgJbVYl" +
                                          "aW0NTmo9vT5w4iL3HsW3poWWyHTVkdaFAB0PncknDa+tLkXv/mPBG6xjDZ1IlaR1IUC3N9UH" +
                                          "8XfYCaj7tNkk+ymtzVPGOMNjYdIzVAjQ7ab+eAObxue02fpALbKONbR2gOlHF7VrHNda42hd" +
                                          "COJH/SnMevEBXpxkvBCorZS0LgTcoJepPyZJ/cmLrxgvBGoHWcOTwi7+vZ4+OSRJneTFCuOF" +
                                          "oBPJUxJmeCxMeoas0QyfXAfdMer3iulhXvzBOoVAbaWktTWwDD2gtU4Q3yZJ7ckKin8H0Noq" +
                                          "SWsryEk5p/02LcQ/W7LCZMW0Ex+FTJtxVsNPjkEDWv+p7FyFglezYLKCF3grQ2OR6XKUGdWW" +
                                          "Bj2t14S7MTlPTuB5DW7k8AcZBHorcp/JCcwoX6LyW3LnMVbvdXx1gzxJp91doMzcqZIUX2/T" +
                                          "E2fLryleGG+UJyIfY7xAPcYZU8/YKUeODH4G+ykvmGfi/PQ2jhs3Mf3RwDHkCohi8NO8yZxw" +
                                          "P57MDuyELme6k0G2T81AXeIEnevxHtwln6bSIQkaGi3Nnm+yLf1ChWEnQ36pHbeYzvRhSqos" +
                                          "aV0fTEmVJa3rgympsqR1fTAlVZa0rg+mpMqS1vXBlFRZ0ro+mJIqS1rXB1NSZUnr+mBKqixp" +
                                          "XR/WOpJPSvGjdX3Alqa2n3Kmiqr2gHPxlPKYxc+jdmg0/geHisKVwcdXlgAAAABJRU5ErkJggg==";

        public const string Base64Icon3 =
            "iVBORw0KGgoAAAANSUhEUgAAADUAAAAwCAYAAACxKzLDAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAATdSURBVGhDzVlNiB" +
            "xFFN6j4B8KYkSEENfdqaoxBxWNCCIIigiCohgEQdSDxn/MQSGQQ04BleBBDxoUFBUPUaMuCsIeNP5GQQwo5LAk/sxU9ey6M93TuwFh" +
            "/V7Pq56aTO9O9XTP9n7wsTuv3vv6va7XVdUzU2Vibfnqi0Ij7u8G6s3IyB/Bha6REf4ugcfx/4eRFq+ERt7HIVsX3UBc1g3kfvBvJL" +
            "/mxUCeoJgzi0qyzNZB1BTPZhcj5jEr7yH5l6KWOgSfo0khRsVn+2J297FctaA7jEQ/cpPrGnEMxTzzb6O2nd2GAL9tHSNfwN/f3Vhw" +
            "jl2qARVEd91J6ItYi7t52AtrC9vPCbV8DLErqU4gl3l4czFUUKAO8tBYiFryVug0Uj20LQ9tHtyWo9WMzYUB3R+sLrUnmycPWhTshc" +
            "NAfszm0hBp2Ur1m2J3vFi/gocmg2Zz57m42EJyUS3PtJvyBh4qDbGu3WiLcnga+96nE9nb0BIP2QuhVfazuXSQtlPQANHu34IPs2tx" +
            "QPRzFg6Xdf1KNpcO2g7CRu0WzMzTyclEi1/dwpIc0PodrW7ikPEBob9IEMvw62zaNKwaNR0b+RQKPTlQoJZ72SU/Vo24ygrhRPAEmw" +
            "dAPY/xufSC+TiHreEBlloX8NuG67zqxo71KHT+manhjrxjRbI22Y2egzz03SLo1OLGeS9a8aLYhU02LcayG9SuZZcE9OCe7VOEXS32" +
            "sPRIOHEL7ba6mM3ZCI064ATQHTyJfWNf1oma7q7rW5S+s0WA7x1O7GE2DyMy6pDj+Bt6+Elj5Hk8PATHtzSytBdCrV6zcVgt72JzH+" +
            "jVt1MHI79e9Vi6rX+ZZGkvdILZWTwm9BJKOX/D5h4GW0591v5zRI8y+jHlkaW94S5UeDe7OTG2aclOqxXHEqMnrFiZZGlvtJtY1DiW" +
            "nv3E6D5Hsa7fkxg9YePKJEvnAuL4pVN9iakT1+BZ+i8xBPJd9vGGTaRMsnQuYME4wvG/UFGPWrF4jPOUjXWZZ5fPiuehXKDjG8Xi2q" +
            "enMDsvp4K6fin7eCONHaCYp8JGcp09jqVzgfQ4fgVJifnkgxaGx3PBJlImWToXsHq/QbEo7hQl1UnEtPyex3PBJjLISmbqE44/jldo" +
            "YZIPgTzB47lgE3FJCfPwSGTF85A3lpZ2XIhJOUWxeIs4Quenn+kDjkNt9skFN5myyNLeCLV43MbGRt1LRR22BnonYj9v2NgyydJeoD" +
            "flfqz4KTGu9L536xnRgnm/2+4L9rlZ7UevQW5c3HLeq2B4Kx1EYWGgdvPQSKRxA6QV1ZfD8Sy9LuLWzOX0Bu7GoOOGv5BxdmTLBhy/" +
            "wwHx/d5KJR6JtLqdZnJtcfoCDptY+wXB7Pn0a8pqUJuhGaEvY7AYPE8rJh6T3orN3LAz6GswFPGHG7C1qT5A4Xdy+huj3ZDXI+hF3J" +
            "mvhoWqJWblaBSo57pB/TpOdzzQ8Slaqu9c1rXbsK89iGL3Ji0JZl24KBNtLfbQakxtRy0fNqYv4XQmj6ykipKlq0NWUkXJ0tUhK6mi" +
            "ZOnqkJVUUbJ0dchKqihZujpkJVWULF0dspIqSpauDllJFSVLV4espIqSpasDHTCzEhuXpMfS1YGO/FnJjcs8P+VMFGWdAbfELLmgwy" +
            "cSm+jPo36YmvofkMuYq9S1YjQAAAAASUVORK5CYII=";

        public const string Base64Icon4 =
            "iVBORw0KGgoAAAANSUhEUgAAADUAAAAwCAYAAACxKzLDAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAS1SURBVGhDzVlLiBxFGO7DdvXG+EAxJJqp7onGBx48qPhAEEFQRBAUF4MgiHrQ+EYPCkIOngQV8aAHDQqKyh40xgcKQg4an1FINjvdvRNdNyiBIOILgyDE7+/9aqZmp5Kp3u7Z3g8+duev+r76a6q6Hj1BnViIg1NzrW7NdfRKFqtvwHnwb/A3cA84nSbhc/g7RcnqxU/JCWfksdqGZH8Bj/oQ9feLphurC2izetCJo4eR5HBndLgr1erNXIfPZHH4PGI72ZF/huom6knaNQv5hpHMu0sS3J3F0UNpe7LNakOYba/dkMbqcdRNB7RafcQqzUA6JN+6ldTHeaJuYrEX5tvBJEbzHmiPGB94/s7ilYWjQ0+zaFnI48lr4HGo54dpy6KVw5IpN81wZeCL+tr4yvRkePzgomA6tIPh2gDPX41/R6stB85co1k0HuxdH6xFY7LvSKP/zrbCy1hUG9Jk4grTKYsHMYrv42/9e1uqwztMQ2hkG8O1Q7xNOw5+kSXhnaxaHTD8kMZ/zejobIZrh2wHaWviamwNDxYnk0TtZbs2d2TxxJWULB8w+nnRMHyJoRXDgVa0eS6OHsAC0rU6djRNosdYpTw6regcY5Qn4X0MDwBlU7KBmnqlCF2aqNtodUzMFxt3+IKtXdajsK+tzof49Z6JY5Md8RyUodcWIacWW+e9aHXb4eUQ9DpjiG/qYlYpIA/u0jpViLPiVlqPhKWbn20FpzHsBqbDU5ZAhrgrh07XiRrl03bdGui9oedJdH1Pp9V2hofBE7VpYAb3n/tn1wUnsngIVt3aSGsvYKa82NPq6EaG+8CIvNY3jz7reizd/fr1kdZeyDZF50Ejl1Boo88ZXoQ95bB0fjByjhJGUydp7Q17oeroiauKIJds9lbtLoKeMGZ1ktbe6Cwuaot6c+G0n6O5RN1cBD3RM6uRtC4F6Myl85Ogm4QX4Z//GHiDdbxBXa2kdSmkWr0jWjw632NZDO/uG5Y/T/W1fZbZ5V16FpWCHN9Ei7YPSqeeNWYzm4L1rOMNox0gbq/SsVFEXeceR+tSoJ/oj2DVC3cVH3R4mOWlYBKpk7QuBSznLxf6RC1IUn/KB8zJr1heCiaRATYwUtC9R/0eGanD8gGN7Gd5KdBogJIwi0fCpWeRN344KzgF++yCaGXBENPvaPYH65QCtbWS1t7AwNxr6W+Rk8R2K1D6PYClrY209oLclC3tt0WQ792KoEzBsu+2LcMeV2r6yTXI1s3Z9yok8aopkI7lWm1h0UjYpj3KiupLh57Wx0TeWrNRbuADOtcLGbMjWzyEzn2Ja/Zb8s1DdFeqo+tkJLubg5MpG9v0y04PTpJfU9KN0bkyIvIyBu0/inJZMYsV2/C4M4OvwTJbsMr59lwS3cD0jw/c+S/FGeoJHD0+dRg1zZ25jh7B1L2E6S4PP+L4lLXUhRj+a/M4vF1eTcmQF9PS3XAlFt463Ir/p2TaFVN+Q7CO6YwfSxOqg7RuDq6kqpLWzcGVVFXSujm4kqpKWjcHV1JVSevm4EqqKmndHFxJVSWtm4MrqaqkdXNwJVWVtG4OSKKxHwjGhiZ/yhkrajwDNj9KNpDQ2H8e9UMQ/A9+NxALzyPIcgAAAABJRU5ErkJggg==";

        public const string Base64Icon5 =
            "iVBORw0KGgoAAAANSUhEUgAAADUAAAAwCAYAAACxKzLDAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAATNSURBVGhDzVlLiBxFGJ6jYNzd7p7JRkQI+MSDBxWNCCIIigiC4mIQBFEPvh+YQwTBg6eghrDZ7t4hCREUlT1ofC0KggeNzygEJ2Snq2fHjYogEnxhEAT9quermZrZ2pnq7Zrt/eBjZqrq++qvqer6q7srLjEZrXh+mN7lR+KgHyVf+VHaxve/wNNeJI7hc8EPxV58zlCyeVGtf3+uH4vnEOxP4H+WbGSaeXEZbTYPENyT4KrBYGY+9sPkNczMi36c7gsi8Q7KG5i9vwfbBlHyLO3KhfyHvSh9ayDAoxjIE1Pxye1stgq18MQ2LxS7MUsnB7SLbFIO5IAQBP71bkAfBHF6O6utsP1w+ywvSh6E9ozm8xurNxaDA8Jg9rBqXQjmWjfC52flJ5ctqzYOA0tugcWFAa8vla9cniweP9Ch3BQ6HcfiCIudAb6/9vybO5EezmfVeDD9wvGzmXdkp/8Ec8k1rHIGLO1r1aA0ngpi8S4+3ee2IEzu7XaE/MJi52C+GxyY6vcz8D42LQ5s1e/T/M/JWXEBi51DpoOp/eIGbECPoy95MjneHRTpxekRL166jpL1A2Y/SkMkyphFG4ZamF4YROlj2BmFPjj80bvYJD9q862LlBFOBo+wuA+omwEXVbucXETOuptWa6IWtreh3Wyfdj2XQrC/eSn+oVeUiSnJDr0O8tEqRchTi66z3rTQeIc+GMVqlF7JJhnkhTvYpgiRox6m9Uj0dGl74uAPPovNwMX5fF9HWMvy0Gk6UaN+QW/rgNYJHQeBW7q6UBxi8WrIE7XWwXdB2HwUh9AtrF4Fra0z0toK+LMjpZuKxG0s7gHT+HLPPPlksj566+61d0daW6EaLV2CuE9T+ymLO+hfcul7E3tPDF+jRE/jjrS2hr5RTc0l12eFnS1bjTY9mhVaQpm5JK2tAc0Ope3ecOrXURC27sgKLaF0LknrXMBsqZvODyvVurgCX/6VBTg4vso21qCRU9I6F5C73pRaHA6+lUE9oMxwpsp9nlLaPubI8iY9q3JBHt+oPyVNX1JmW/ctT7ONNZRWp7x7zS7eUVwjx9E6F+gn9WeQwBAAfiCT/8L6XFCBuCStcwG6A5k+Tlfkjz+yH6H4gvW5kGkHWMZM4Vp6W2rR97GKnCGaNVifCyqQPiJgVo+ESc8qa3j11iR0K5keG0bFi8U3NPudbXKBWqektTUwhoe6+lDcKS+wQ5ph7ucAmtYZaW0Feafc1Ybi66wwmM+euynDhukkPgyatscNWn7yNkjXBfV2774KO8ZhrbKBfLWTVSOh6bqUG4UtTXpar4kgbp4n78D7dKYHMioja5RPTT/HQff1bKcKxf34vDl7QjsrJigb2/Kr7lk6R75Nqc61LpYzIh/G+GHzadRjx0w6O7bisJXReQyWLPUJNjff8KLWrQx/ODC9V2NpPOPHyUcGo1KZvRKKxVPV+dZVDHd92HpgedqLli/HErwJh9575KOpbElKGjouTPjKZxX4PpMtOyz5LbOixnDGj1UBOSCty4MpqKKkdXkwBVWUtC4PpqCKktblwRRUUdK6PJiCKkpalwdTUEVJ6/JgCqooaV0eTEEVJa3LA4Io7QXB2IAjTWmvcsYKh2fA8mdJBwIa++tRO1Qq/wNhjFjPCSHmWAAAAABJRU5ErkJggg==";

        #endregion



        /// <summary>
        /// cation: it is a shallow
        /// </summary>
        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
