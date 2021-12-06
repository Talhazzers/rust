using Network;

namespace Oxide.Plugins
{
    [Info("Ekiphost Anti Cheat", "Ekiphost", "1.0.0")]
    [Description("Ekiphost tarafından ücretsiz olarak sağlanan anti hile eklentisi.")]
    class EkiphostAntiCheat : RustPlugin
    {
        string permBypass = "ekiphostanticheat.bypass";
        private void Init()
        {
            permission.RegisterPermission(permBypass, this);
        }
        object CanClientLogin(Network.Connection connection)
        {
            if (connection == null) return true;
            var steamid = connection.userid.ToString();
            var ip = connection.ipaddress.Split(':')[0];
            if (!permission.UserHasPermission(connection.userid.ToString(), permBypass)) {
                webrequest.Enqueue("https://api.ekiphost.com/rust/kontrol?steamid="+steamid+"&ip="+ip, null, (code, response) => {
                    if (response == "1") {
                        Puts("[!] "+connection.username+"("+steamid+") adlı oyuncunun sunucuya girişi engellendi. IP: "+ip);
                        Net.sv.Kick(connection, "Güvenlik sistemi tarafından engellendiniz.");
                    }
                }, this); 
            }
            return true;
        }
        void OnPlayerRespawn(BasePlayer player)
        {
            SendReply(player, "<size=12>Bir oyuncunun hile olduğunu düşünüyorsanız <color=#70CF63>F7</color>'den raporlayabilirsiniz.\nTüm raporlarınız incelenmektedir. Gereksiz yere oyuncuları raporlamanızın sonucu <color=#D36464>cezalandırılmanız</color> ile sonuçlanabilir.</size>");
        }
        void OnPlayerReported(BasePlayer reporter, string targetName, string targetId, string subject, string message, string type)
        {
            var supheli = Player.FindById(targetId);
            var raporlayan = reporter.userID;
            if (!permission.UserHasPermission(supheli.userID.ToString(), permBypass) || reporter == null || type != "cheat" || !Player.IsConnected(supheli)) return;
            webrequest.Enqueue("https://api.ekiphost.com/rust/risk?steamid="+supheli.userID, null, (code, response) => {
                if (response == "1") {
                    Puts("[!] Riskli bir profile sahip olan "+targetName+" adlı oyuncu "+reporter.displayName+" tarafından raporlandı.");
                    Puts("[!] Rapor sebebi: "+subject);
                    webrequest.Enqueue("https://api.ekiphost.com/rust/rapor?server="+ConVar.Server.ip+"&reporter="+raporlayan+"&supheli="+supheli.userID+"&subject="+subject, null, (code2, response2) => {
                        if (response2 == "ban")
                        {
                            webrequest.Enqueue("https://api.ekiphost.com/rust/yasakla?steamid="+supheli.userID+"&ip="+Player.Address(supheli).Split(':')[0], null, (code3, response3) => { }, this);
                            Player.Kick(supheli, "Güvenlik sistemi tarafından engellendiniz.");
                            Puts("[!] "+targetName+" adlı oyuncu anti-hile sistemi tarafından yasaklandı.");
                            Server.Broadcast(targetName+" adlı oyuncu anti-hile sistemi tarafından yasaklandı.");
                        }  
                    }, this);
                }
            }, this);
        }
    }
}