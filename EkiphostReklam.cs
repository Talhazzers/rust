namespace Oxide.Plugins
{
    [Info("Ekiphost Reklam", "Ekiphost", "1.0.0")]
    [Description("Sunucunuzda Ekiphost reklamını bu eklenti ile sağlayabilirsiniz.")]
    public class EkiphostReklam : RustPlugin
    {
        void OnServerInitialized()
        {
            if (!ConVar.Server.description.Contains("Ekiphost"))
            {
                string description = ConVar.Server.description+"\n\nBu sunucu gücünü Ekiphost'dan almaktadır. Sizde Rust sunucusuna sahip olmak istiyorsanız Ekiphost.com'u ziyaret edebilirsiniz.";
                ConVar.Server.description = description;
            }
            timer.Every(1800f, () =>
            {
                Server.Broadcast("Bu sunucu gücünü Ekiphost'dan almaktadır. Sizde Rust sunucusuna sahip olmak istiyorsanız <color=#FFB900>Ekiphost.com</color>'u ziyaret edebilirsiniz.");
            });
        }
    }
}