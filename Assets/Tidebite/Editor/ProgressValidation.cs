using System;
using System.IO;
using Tidebite.Core;
using UnityEditor;
using UnityEngine;

namespace Tidebite.Editor
{
    public static class ProgressValidation
    {
        [MenuItem("Tidebite/Validate progress saves")]
        public static void Run()
        {
            string folder=System.IO.Path.GetFullPath("TestResults/profile-validation/"+Guid.NewGuid().ToString("N"));
            string path=System.IO.Path.Combine(folder,"progress.json");var store=new ProgressStore(path);
            var data=store.Load();Require(data.BestLevel==1&&data.OwnedSkins==1,"New profile");
            data.TotalFood=25;data.TotalEats=3;data.BestLevel=16;data.Discover(0);data.Discover(6);data.ClaimRewards();
            Require(data.SelectFamily(1)&&data.EquipSkin(1),"Unlock/equip");Require(store.Save(data),"First atomic save");
            var first=store.Load();Require(first.TotalFood==25&&first.SelectedFamily==1&&first.SelectedSkin==1&&first.Discovered==65,"JSON round trip");
            data.TotalFood=27;Require(store.Save(data),"Second save");Require(File.Exists(path+".bak"),"Backup exists");
            File.WriteAllText(path,"{broken json");var recovered=store.Load();Require(recovered.TotalFood==25&&recovered.SelectedSkin==1,"Corrupt primary falls back to known-good backup");
            Require(store.Save(recovered),"Recovered profile can be saved");Require(store.Load().TotalFood==25,"Recovery persists");
            store.AppendRun(new RunRecord{reason="death",seconds=42,firstFood=1.5f,firstEat=12,firstEvolution=28});
            store.AppendRun(new RunRecord{reason="round",seconds=240});
            var lines=File.ReadAllLines(path+".runs.jsonl");Require(lines.Length==2&&JsonUtility.FromJson<RunRecord>(lines[0]).firstEat==12,"Local run metrics");
            Debug.Log("TIDEBITE PROGRESS_VALIDATED atomic-save backup-recovery cosmetics collection local-run-metrics");
        }
        private static void Require(bool condition,string message){if(!condition)throw new Exception("Progress validation: "+message);}
    }
}
