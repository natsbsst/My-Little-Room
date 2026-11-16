using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class LifePlayChecks
{
    [MenuItem("My Little Room/Life/Check menus needs and door routes")]
    public static void CheckMenusNeedsAndRoutes()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        Application.runInBackground = true;
        var actor=GameObject.Find("MyLittleRoom_Mannequin");
        actor.GetComponent<LifeBootstrap>().StartCoroutine(MenusNeedsAndRoutes(actor));
    }
    static IEnumerator MenusNeedsAndRoutes(GameObject actor)
    {
        var hud=actor.GetComponent<LifeHUD>(); var clock=UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
        var mode=actor.GetComponent<ControlModeManager>(); var motor=actor.GetComponent<MannequinWalk>();
        var needs=actor.GetComponent<NPCNeeds>(); var actions=actor.GetComponents<NeedAction>();
        Vector3 original=actor.transform.position;
        float[] originalNeeds=LifeBootstrap.Needs.Select(needs.GetValue).ToArray();
        var sample=new GameObject("Needs balance check").AddComponent<NPCNeeds>(); sample.enabled=false;
        try
        {
            Require(hud.View==LifeHUD.ScreenView.MainMenu && Time.timeScale==0,"Start screen should pause the game.");
            float hour=clock.CurrentHour;
            hud.OpenSettings();
            Require(hud.View==LifeHUD.ScreenView.Settings,"Settings open from start screen.");
            yield return new WaitForSecondsRealtime(.3f);
            Require(Mathf.Approximately(hour,clock.CurrentHour),"Clock must stop in menus.");
            hud.BackFromSettings();Require(hud.View==LifeHUD.ScreenView.MainMenu,"Settings return to start screen.");
            hud.StartGame();hud.TogglePause();hud.OpenSettings();hud.BackFromSettings();
            Require(hud.View==LifeHUD.ScreenView.PauseMenu && Time.timeScale==0,"Settings return to paused game.");
            hud.TogglePause();Require(hud.View==LifeHUD.ScreenView.Game && Time.timeScale==1,"Resume returns to game.");
            float food=sample.GetValue(NeedType.Satiety),hygiene=sample.GetValue(NeedType.Hygiene);
            sample.AdvanceHours(1,12);
            Require(food-sample.GetValue(NeedType.Satiety)>hygiene-sample.GetValue(NeedType.Hygiene),"Food must decay faster than hygiene.");
            sample.AdvanceHours(16,12);
            Require(sample.GetValue(NeedType.Sleep)>15 && sample.GetValue(NeedType.Sleep)<30,"Sleep should fall near 20 by late evening, not midday.");
            var sleep=actions.Single(a=>a.ActionName=="Sleep");
            clock.SetTimeOfDay(12);float midday=sleep.GetUtility(needs);
            clock.SetTimeOfDay(22);Require(sleep.GetUtility(needs)>midday,"Sleep has higher priority at night.");
            clock.SetTimeOfDay(hour);clock.ResumeAutomaticCycle();
            Debug.Log("MENUS_NEEDS_CHECKS_PASS: start/settings/pause/resume; frozen clock; hunger rate; late-evening sleep and night priority.");
            if(mode.IsAutoMode)mode.SwitchMode();
            Teleport(actor,actions.Single(a=>a.ActionName=="Relax").InteractionPoint.position);
            foreach(string name in new[]{"Sleep","Relax","Wash","Relax"})
            {
                var action=actions.Single(a=>a.ActionName==name);
                hud.Notice="Проверка проходов: "+name;
                Require(motor.SetDestination(action.InteractionPoint.position),"Complete route to "+name);
                float deadline=Time.realtimeSinceStartup+25;
                while(motor.IsNavigating && Time.realtimeSinceStartup<deadline)yield return null;
                Require(Vector3.ProjectOnPlane(actor.transform.position-action.InteractionPoint.position,Vector3.up).magnitude<.65f,"Physical route reaches "+name+" through doorways.");
                motor.StopNavigation();
                Debug.Log("DOOR_ROUTE_PASS: "+name);
                yield return new WaitForSeconds(.5f);
            }
            Debug.Log("DOOR_ROUTES_CHECKS_PASS: sofa-bed-sofa-shower-sofa, with physical door leaf carving.");
        }
        finally
        {
            UnityEngine.Object.Destroy(sample.gameObject);
            motor.StopNavigation();Teleport(actor,original);
            for(int i=0;i<originalNeeds.Length;i++)needs.ChangeNeed(LifeBootstrap.Needs[i],originalNeeds[i]-needs.GetValue(LifeBootstrap.Needs[i]));
            hud.Notice=null;
        }
    }
    [MenuItem("My Little Room/Life/Check jump in Play Mode")]
    public static void CheckJump()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        var actor = GameObject.Find("MyLittleRoom_Mannequin");
        Application.runInBackground = true;
        actor.GetComponent<LifeBootstrap>().StartCoroutine(JumpChecks(actor));
    }
    static IEnumerator JumpChecks(GameObject actor)
    {
        var motor = actor.GetComponent<MannequinWalk>();
        var body = actor.GetComponent<CharacterController>();
        var hud = actor.GetComponent<LifeHUD>();
        var mode = actor.GetComponent<ControlModeManager>();
        if (mode.IsAutoMode) mode.SwitchMode();
        if (hud.IsPaused) hud.TogglePause();
        yield return new WaitForSeconds(.2f);
        float startY = actor.transform.position.y;
        Require(body.isGrounded, "Jump starts on the floor.");
        hud.TogglePause();
        bool pausedJump = motor.TryJump();
        hud.TogglePause();
        Require(!pausedJump, "Jump is blocked on pause.");
        mode.SwitchMode();
        Require(mode.IsAutoMode, "AUTO must enable for the jump test.");
        bool autoJump = motor.TryJump();
        mode.SwitchMode();
        Require(!autoJump, "Jump is blocked in AUTO.");
        Require(motor.TryJump(), "Grounded jump should start.");
        Require(!motor.TryJump(), "A second jump must not start in the same frame.");
        float apex = startY;
        bool airborne = false;
        float deadline = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            apex = Mathf.Max(apex, actor.transform.position.y);
            if (!body.isGrounded) { airborne = true; Require(!motor.TryJump(), "No double jump in midair."); }
            if (airborne && body.isGrounded) break;
        }
        Require(airborne && apex - startY > .25f, "Player must rise above the floor.");
        Require(body.isGrounded && Mathf.Abs(actor.transform.position.y - startY) < .12f, "Player must land on the original floor.");
        Debug.Log("JUMP_CHECKS_PASS: jump rises and lands; double jump, pause and AUTO blocked. Height=" + (apex-startY));
    }
    [MenuItem("My Little Room/Life/Check character in Play Mode")]
    public static void Check()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        var actor=GameObject.Find("MyLittleRoom_Mannequin");
        actor.GetComponent<LifeBootstrap>().StartCoroutine(Run(actor));
    }
    static void Require(bool test,string message) { if (!test) throw new InvalidOperationException("LIFE_CHECKS_FAIL: "+message); }
    static void Teleport(GameObject actor,Vector3 point)
    {
        actor.GetComponent<MannequinWalk>().StopNavigation();
        var body=actor.GetComponent<CharacterController>();
        body.enabled=false; actor.transform.position=point; body.enabled=true; Physics.SyncTransforms();
    }
    static IEnumerator Run(GameObject actor)
    {
        var needs=actor.GetComponent<NPCNeeds>(); var mode=actor.GetComponent<ControlModeManager>();
        var manual=actor.GetComponent<ManualInteraction>(); var hud=actor.GetComponent<LifeHUD>();
        var actions=actor.GetComponents<NeedAction>();
        Vector3 original=actor.transform.position; Quaternion rotation=actor.transform.rotation;
        float[] values=LifeBootstrap.Needs.Select(needs.GetValue).ToArray();
        bool passed=false;
        try
        {
            Require(actions.Length==5,"Expected all five furniture actions, got "+actions.Length);
            Require(actions.All(a=>a.IsAvailable()),"Every furniture point must be reachable on the character NavMesh.");
            Require(actor.GetComponent<CharacterController>().enabled,"CharacterController must remain enabled.");
            if(hud.IsPaused)hud.TogglePause(); if(mode.IsAutoMode)mode.SwitchMode();
            hud.Notice="Проверка: пять действий и потребности…";
            foreach(var action in actions)
            {
                needs.ChangeNeed(action.AffectedNeed,30-needs.GetValue(action.AffectedNeed));
                Teleport(actor,action.InteractionPoint.position);
                yield return null;
                Require(manual.GetAvailableAction()==action,"Nearest action must match "+action.ActionName);
                Require(manual.TryPerform(),"Manual interaction should start: "+action.ActionName);
                float timeout=Time.realtimeSinceStartup+6;
                while(manual.IsBusy && Time.realtimeSinceStartup<timeout) yield return null;
                Require(!manual.IsBusy && needs.GetValue(action.AffectedNeed)>65,"Manual action must restore need only on completion: "+action.ActionName);
            }
            var eat=actions.Single(a=>a.ActionName=="Eat");
            Teleport(actor,eat.InteractionPoint.position);yield return null;
            needs.ChangeNeed(NeedType.Satiety,30-needs.GetValue(NeedType.Satiety));
            Require(manual.TryPerform(),"Cancellation test should start.");
            Teleport(actor,original);yield return null;yield return null;
            Require(!manual.IsBusy && needs.GetValue(NeedType.Satiety)<35,"Moving away must cancel without restoring need.");
            hud.Notice="Проверка: пауза и автономное движение…";
            hud.TogglePause(); float before=needs.GetValue(NeedType.Sleep);
            yield return new WaitForSecondsRealtime(.3f);
            Require(Mathf.Approximately(before,needs.GetValue(NeedType.Sleep)),"Needs must stop decaying on pause.");
            hud.TogglePause();
            foreach(var type in LifeBootstrap.Needs)needs.ChangeNeed(type,90-needs.GetValue(type));
            needs.ChangeNeed(NeedType.Satiety,5-needs.GetValue(NeedType.Satiety));
            mode.SwitchMode(); Require(mode.IsAutoMode,"AUTO must enable.");
            float deadline=Time.realtimeSinceStartup+45;
            while(needs.GetValue(NeedType.Satiety)<30 && Time.realtimeSinceStartup<deadline) yield return null;
            Require(needs.GetValue(NeedType.Satiety)>30,"Utility AI must walk to food and complete Eat.");
            mode.SwitchMode();
            Require(!mode.IsAutoMode && actor.GetComponent<CharacterController>().enabled && !actor.GetComponent<MannequinWalk>().IsNavigating,"Return to MANUAL must stop AI while preserving physics.");
            passed=true;
            Debug.Log("LIFE_CHECKS_PASS: 5 reachable actions; manual recovery; cancellation; pause; AUTO path and recovery; MANUAL return; CharacterController preserved.");
        }
        finally
        {
            if(hud.IsPaused)hud.TogglePause();if(mode.IsAutoMode)mode.SwitchMode();
            Teleport(actor,original);actor.transform.rotation=rotation;
            for(int i=0;i<values.Length;i++)needs.ChangeNeed(LifeBootstrap.Needs[i],values[i]-needs.GetValue(LifeBootstrap.Needs[i]));
            hud.Notice=passed ? "Проверка пройдена — можно играть" : "Проверка не пройдена — см. Console";
        }
    }
}
