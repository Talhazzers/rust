using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using Network;

namespace Oxide.Plugins
{
    [Info("Ekiphost Anti Cheat", "Ekiphost", "1.0.2")]
    [Description("Ekiphost tarafından ücretsiz olarak sağlanan anti hile eklentisi.")]
    class EkiphostAntiCheat : RustPlugin
    {
        private HashSet<Tuple<Vector3, Quaternion>> _spawnData = new HashSet<Tuple<Vector3, Quaternion>>();
        private const int ScanHeight = 100;
        private static int GetBlockMask => LayerMask.GetMask("Construction", "Prevent Building", "Water");
        private static bool MaskIsBlocked(int mask) => GetBlockMask == (GetBlockMask | (1 << mask));
        private const string StashPrefab = "assets/prefabs/deployable/small stash/small_stash_deployed.prefab";
        private Dictionary<MonumentInfo, float> monuments { get; set; } = new Dictionary<MonumentInfo, float>();
        string permBypass = "ekiphostanticheat.bypass";
        private void CanSeeStash(BasePlayer player, StashContainer stash)
        {
            if (player.userID == stash.OwnerID) return;
            if (player.IsAdmin) { return; }
            if (stash.OwnerID > 0 && player.currentTeam > 0) { if (player.Team != null && player.Team.members.Contains(stash.OwnerID)) { return; } }
            webrequest.Enqueue("https://api.ekiphost.com/rust/stash?server="+ConVar.Server.ip+"&supheli="+player.userID, null, (code, response) => {}, this); 
        }
        void OnServerInitialized()
        {
            permission.RegisterPermission(permBypass, this);
            GenerateTraps();
        }
        void Unload()
        {
            ClearTraps();
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
        private float eskican;

        private void OnPlayerLand(BasePlayer player) { eskican = player.health; }

        private void OnPlayerLanded(BasePlayer player, float num)
        {
            if (player.health - eskican == 0 && !player.IsAdmin)
            {
                webrequest.Enqueue("https://api.ekiphost.com/rust/land?float="+num+"&server="+ConVar.Server.ip+"&supheli="+player.userID, null, (code, response) => {}, this); 
            }
        }
                bool IsOnRoad(Vector3 target)
        {
            RaycastHit hitInfo;
            if (!Physics.Raycast(target, Vector3.down, out hitInfo, 66f, LayerMask.GetMask("Terrain", "World", "Construction", "Water"), QueryTriggerInteraction.Ignore) || hitInfo.collider == null) return false;
            if (hitInfo.collider.name.ToLower().Contains("road")) return true;
            return false;
        }
        private bool IsMonumentPosition(Vector3 target)
        {
            foreach (var monument in monuments) { if (InRange(monument.Key.transform.position, target, monument.Value)) { return true; } }
            return false;
        }
        private void SetupMonuments()
        {
            foreach (var monument in TerrainMeta.Path?.Monuments?.ToArray() ?? UnityEngine.Object.FindObjectsOfType<MonumentInfo>()) {
                if (string.IsNullOrEmpty(monument.displayPhrase.translated)) {
                    float size = monument.name.Contains("power_sub") ? 35f : Mathf.Max(monument.Bounds.size.Max(), 75f);
                    monuments[monument] = monument.name.Contains("cave") ? 75f : monument.name.Contains("OilrigAI") ? 150f : size;
                } else {
                    monuments[monument] = GetMonumentFloat(monument.displayPhrase.translated.TrimEnd());
                }
            }
        }
        private float GetMonumentFloat(string monumentName)
        {
            switch (monumentName){case "Abandoned Cabins": return 54f; case "Abandoned Supermarket": return 50f; case "Airfield": return 200f; case "Bandit Camp": return 125f; case "Giant Excavator Pit": return 225f; case "Harbor": return 150f; case "HQM Quarry": return 37.5f; case "Large Oil Rig": return 200f; case "Launch Site": return 300f; case "Lighthouse": return 48f; case "Military Tunnel": return 100f; case "Mining Outpost": return 45f; case "Oil Rig": return 100f; case "Outpost": return 250f; case "Oxum's Gas Station": return 65f; case "Power Plant": return 140f; case "Satellite Dish": return 90f; case "Sewer Branch": return 100f; case "Stone Quarry": return 27.5f; case "Sulfur Quarry": return 27.5f; case "The Dome": return 70f; case "Train Yard": return 150f; case "Water Treatment Plant": return 185f; case "Water Well": return 24f; case "Wild Swamp": return 24f;}
            return 300f;
        }
        private static bool InRange(Vector3 a, Vector3 b, float distance, bool ex = true)
        {
            if (!ex) { return (a - b).sqrMagnitude <= distance * distance; }
            return (new Vector3(a.x, 0f, a.z) - new Vector3(b.x, 0f, b.z)).sqrMagnitude <= distance * distance;
        }
        private int ClearTraps()
        {
            var counter = 0;
            foreach (var trap in UnityEngine.Object.FindObjectsOfType<StashContainer>()) { if (trap != null && trap.OwnerID == 0) { trap.Kill(); counter++; } }
            return counter;
        }
        private int GenerateTraps()
        {
            var counter = 0;
            var neededCount = 150;
            for (var i = 0; i < neededCount; i++) {
                var spawnData = GetValidSpawnData();
                if (spawnData.Item1 == Vector3.zero) { GeneratePositions(); }
                spawnData = GetValidSpawnData();
                var box = GameManager.server.CreateEntity(StashPrefab, spawnData.Item1, spawnData.Item2);
                if (box is StashContainer) { box.Spawn(); SetTrap(box as StashContainer); counter++; }
            }
            return counter;
        }

        private void SetTrap(StashContainer stashContainer)
        {
            stashContainer.inventory.Clear();
            var items = stashContainer.inventory.itemList.ToList();
            for (var i = 0; i < items.Count; i++) { items[i].DoRemove(); }
            ItemManager.CreateByName("scrap", Random.Range(5, 125)).MoveToContainer(stashContainer.inventory);
            stashContainer.SetHidden(true);
            stashContainer.CancelInvoke(stashContainer.Decay);
        }
        private void GeneratePositions()
        {
            _spawnData.Clear();
            var generationSuccess = 0;
            var islandSize = ConVar.Server.worldsize / 2;
            for (var i = 0; i < 150 * 6; i++) {
                if (generationSuccess >= 150 * 2) { break; }
                var x = Core.Random.Range(-islandSize, islandSize);
                var z = Core.Random.Range(-islandSize, islandSize);
                var original = new Vector3(x, ScanHeight, z);
                while (IsMonumentPosition(original) || IsOnRoad(original)) {
                    x = Core.Random.Range(-islandSize, islandSize);
                    z = Core.Random.Range(-islandSize, islandSize);
                    original = new Vector3(x, ScanHeight, z);
                }
                var data = GetClosestValidPosition(original);
                if (data.Item1 != Vector3.zero) { _spawnData.Add(data); generationSuccess++; }
            }
        }
        private Tuple<Vector3, Quaternion> GetClosestValidPosition(Vector3 original)
        {
            var target = original - new Vector3(0, 200, 0);
            RaycastHit hitInfo;
            if (Physics.Linecast(original, target, out hitInfo) == false) { return new Tuple<Vector3, Quaternion>(Vector3.zero, Quaternion.identity); }
            var position = hitInfo.point;
            var collider = hitInfo.collider;
            var colliderLayer = 4;
            if (collider != null && collider.gameObject != null) { colliderLayer = collider.gameObject.layer; }
            if (collider == null) { return new Tuple<Vector3, Quaternion>(Vector3.zero, Quaternion.identity); }
            if (MaskIsBlocked(colliderLayer) || colliderLayer != 23) { return new Tuple<Vector3, Quaternion>(Vector3.zero, Quaternion.identity); }
            if (IsValidPosition(position) == false) { return new Tuple<Vector3, Quaternion>(Vector3.zero, Quaternion.identity); }
            var rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal) * Quaternion.Euler(Vector3.zero);
            return new Tuple<Vector3, Quaternion>(position, rotation);
        }
        private Tuple<Vector3, Quaternion> GetValidSpawnData()
        {
            if (!_spawnData.Any()) { return new Tuple<Vector3, Quaternion>(Vector3.zero, Quaternion.identity); }
            for (var i = 0; i < 25; i++) {
                var number = Core.Random.Range(0, _spawnData.Count);
                var spawnData = _spawnData.ElementAt(number);
                _spawnData.Remove(spawnData);
                if (IsValidPosition(spawnData.Item1))
                    return spawnData;
            }
            return new Tuple<Vector3, Quaternion>(Vector3.zero, Quaternion.identity);
        }
        private bool IsValidPosition(Vector3 position)
        {
            var entities = new List<BuildingBlock>();
            Vis.Entities(position, 25, entities);
            return entities.Count == 0;
        }
    }
}