using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LifeHUD : MonoBehaviour
{
    public enum ScreenView { Game, MainMenu, PauseMenu, Settings }
    public ScreenView View { get; private set; }
    ScreenView settingsReturn;
    NPCNeeds needs;
    ControlModeManager mode;
    UtilityAI ai;
    ManualInteraction manual;
    AudioSource uiAudio;
    AudioClip click;
    bool started;
    static bool launchNextScene;
    public bool IsPaused => View != ScreenView.Game;
    public bool UsesCityBackdrop => View==ScreenView.MainMenu || (View==ScreenView.Settings && settingsReturn==ScreenView.MainMenu);
    public string Notice { get; set; }
    readonly string[] labels = { "Сытость", "Гигиена", "Сон", "Работа", "Досуг" };
    GUIStyle text, title, small, button, heading, panelTitle, strategyButton;
    Texture2D panelTexture, buttonTexture, hoverTexture, gearTexture;
    float volume;
    static readonly Color Panel = new Color(.29f, .275f, .34f, .98f);
    void Awake()
    {
        needs=GetComponent<NPCNeeds>(); mode=GetComponent<ControlModeManager>();
        ai=GetComponent<UtilityAI>(); manual=GetComponent<ManualInteraction>();
        volume=PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume=volume;
        uiAudio=gameObject.AddComponent<AudioSource>();
        uiAudio.playOnAwake=false; uiAudio.ignoreListenerPause=true; uiAudio.volume=.5f;
        click=Resources.Load<AudioClip>("LifeAudio/MyLittleLife_UI_Click_SoftChime");
        if (!Application.isEditor) Screen.fullScreen=PlayerPrefs.GetInt("Fullscreen",1)==1;
        started=launchNextScene;
        launchNextScene=false;
        SetView(started ? ScreenView.Game : ScreenView.MainMenu);
        if(!GetComponent<CityMenuBackdrop>())gameObject.AddComponent<CityMenuBackdrop>();
    }
    void Update()
    {
        if (!Application.isFocused || Keyboard.current==null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (View==ScreenView.Settings) BackFromSettings();
        else if (View==ScreenView.Game || View==ScreenView.PauseMenu) TogglePause();
    }
    void SetView(ScreenView view)
    {
        View=view; Time.timeScale=IsPaused ? 0f : 1f; AudioListener.pause=IsPaused;
        Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
    }
    public void StartGame()
    {
        if(started) { launchNextScene=true; Time.timeScale=1; AudioListener.pause=false; SceneManager.LoadScene(gameObject.scene.name); return; }
        started=true; Notice=null; SetView(ScreenView.Game);
    }
    public void TogglePause()
    {
        if (View==ScreenView.Settings) { BackFromSettings(); return; }
        if (View==ScreenView.MainMenu) { StartGame(); return; }
        SetView(View==ScreenView.Game ? ScreenView.PauseMenu : ScreenView.Game);
    }
    public void OpenSettings() { settingsReturn=View; SetView(ScreenView.Settings); }
    public void BackFromSettings() => SetView(settingsReturn);
    public void ReturnToMainMenu()
    {
        if(mode.IsAutoMode)mode.SwitchMode();
        SetView(ScreenView.MainMenu);
    }
    void OnDisable() { Time.timeScale=1f; AudioListener.pause=false; }
    void OnDestroy()
    {
        if(panelTexture)Destroy(panelTexture); if(buttonTexture)Destroy(buttonTexture); if(hoverTexture)Destroy(hoverTexture);
        if(gearTexture)Destroy(gearTexture);
    }
    static Texture2D Solid(Color color)
    {
        var texture=new Texture2D(1,1); texture.SetPixel(0,0,color); texture.Apply(); return texture;
    }
    static Texture2D GearIcon()
    {
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            float dx=x-31.5f,dy=y-31.5f,r=Mathf.Sqrt(dx*dx+dy*dy);
            float outer=Mathf.Cos(Mathf.Atan2(dy,dx)*8f)>.3f ? 28f : 22f;
            float alpha=Mathf.Clamp01(outer-r)*Mathf.Clamp01(r-10f);
            texture.SetPixel(x,y,new Color(1,1,1,alpha));
        }
        texture.Apply();return texture;
    }
    void Styles()
    {
        if(text!=null)return;
        text=new GUIStyle(GUI.skin.label) { fontSize=18, normal={textColor=new Color(.97f,.94f,.98f)} };
        small=new GUIStyle(text) {fontSize=15,wordWrap=true};
        title=new GUIStyle(text) {fontSize=34,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
        heading=new GUIStyle(text) {fontSize=21,fontStyle=FontStyle.Bold};
        panelTitle=new GUIStyle(heading) {alignment=TextAnchor.MiddleCenter};
        panelTexture=Solid(Panel); buttonTexture=Solid(new Color(.70f,.50f,.63f)); hoverTexture=Solid(new Color(.82f,.64f,.76f));
        gearTexture=GearIcon();
        button=new GUIStyle(GUI.skin.button) {fontSize=20,fontStyle=FontStyle.Bold,
            normal={background=buttonTexture,textColor=Color.white},hover={background=hoverTexture,textColor=Color.white},
            active={background=hoverTexture,textColor=Color.white},border=new RectOffset(),padding=new RectOffset(14,14,8,8)};
        strategyButton=new GUIStyle(button) {fontSize=16};
    }
    bool Button(Rect rect,string label,GUIStyle style=null)
    {
        if(!GUI.Button(rect,label,style ?? button))return false;
        if(click)uiAudio.PlayOneShot(click);
        return true;
    }
    void OnGUI()
    {
        if(!needs || !mode)return;
        Styles();
        float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        float width=Screen.width/scale,height=Screen.height/scale;
        if(View==ScreenView.Game) DrawGame(width,height);
        else DrawMenu(width,height);
    }
    void DrawGame(float width,float height)
    {
        float panelX=width-296;
        GUI.DrawTexture(new Rect(panelX,20,276,252),panelTexture);
        GUI.Label(new Rect(panelX+18,34,240,26),mode.IsAutoMode ? "Режим: AUTO" : "Режим: MANUAL",text);
        for(int i=0;i<labels.Length;i++)
        {
            float value=needs.GetValue(LifeBootstrap.Needs[i]),y=66+i*37;
            GUI.Label(new Rect(panelX+18,y,238,22),labels[i]+"     "+Mathf.RoundToInt(value)+" / 100",small);
            Color old=GUI.color;
            GUI.color=new Color(.18f,.18f,.23f);GUI.DrawTexture(new Rect(panelX+18,y+24,238,5),Texture2D.whiteTexture);
            GUI.color=value<25 ? new Color(.95f,.52f,.54f) : new Color(.65f,.84f,.74f);
            GUI.DrawTexture(new Rect(panelX+18,y+24,238*value/100,5),Texture2D.whiteTexture);GUI.color=old;
        }
        if(Button(new Rect(20,20,48,44),""))TogglePause();
        GUI.DrawTexture(new Rect(28,26,32,32),gearTexture);
        var available=manual.GetAvailableAction();
        var performing=GetComponents<NeedAction>().FirstOrDefault(a=>a.IsPerforming);
        string status=manual.IsBusy ? Translate(manual.CurrentActionName)+" — "+Mathf.RoundToInt(manual.ActionProgress*100)+"%" :
            mode.IsAutoMode ? "AUTO: "+Translate(ai.CurrentAction)+(performing ? " — "+Mathf.RoundToInt(performing.Progress*100)+"%" : "") :
            available ? "E — "+Translate(available.ActionName) : "";
        if(status.Length>0) {GUI.DrawTexture(new Rect(width/2-240,height-68,480,42),panelTexture);GUI.Label(new Rect(width/2-220,height-60,440,30),status,text);}
        if(!string.IsNullOrEmpty(Notice))GUI.Label(new Rect(width/2-260,24,520,40),Notice,small);
    }
    void DrawMenu(float width,float height)
    {
        Color old=GUI.color;GUI.color=new Color(.15f,.13f,.19f,UsesCityBackdrop ? .25f : .82f);GUI.DrawTexture(new Rect(0,0,width,height),Texture2D.whiteTexture);GUI.color=old;
        bool settings=View==ScreenView.Settings;
        float w=settings ? 940 : 520,h=settings ? 570 : 500;
        float x=(width-w)/2,y=(height-h)/2;
        if(View!=ScreenView.PauseMenu)
            GUI.DrawTexture(new Rect(x,y,w,h),panelTexture);
        GUI.Label(new Rect(x+25,y+35,w-50,60),settings ? "НАСТРОЙКИ" : View==ScreenView.MainMenu ? "MY LITTLE LIFE" : "МЕНЮ",title);
        if(settings)
        {
            GUI.Label(new Rect(x+44,y+124,340,30),"Громкость: "+Mathf.RoundToInt(volume*100)+"%",heading);
            float next=GUI.HorizontalSlider(new Rect(x+44,y+176,330,24),volume,0,1);
            if(!Mathf.Approximately(next,volume)) {volume=next;AudioListener.volume=volume;PlayerPrefs.SetFloat("MasterVolume",volume);PlayerPrefs.Save();}
            bool fullscreen=GUI.Toggle(new Rect(x+44,y+225,330,30),Screen.fullScreen,"Полный экран");
            if(fullscreen!=Screen.fullScreen) {Screen.fullScreen=fullscreen;PlayerPrefs.SetInt("Fullscreen",fullscreen?1:0);PlayerPrefs.Save();}
            GUI.Label(new Rect(x+44,y+280,350,30),"Выбор действий в AUTO",heading);
            string strategy=ai.CurrentStrategy==UtilitySelectionStrategy.FirstOnTie ? "Приоритет: сверху списка" : "Приоритет: снизу списка";
            if(Button(new Rect(x+44,y+324,330,40),strategy,strategyButton))
                ai.SetStrategy(ai.CurrentStrategy==UtilitySelectionStrategy.FirstOnTie ? UtilitySelectionStrategy.LastOnTie : UtilitySelectionStrategy.FirstOnTie);
            GUI.Label(new Rect(x+44,y+378,350,92),"Если несколько действий одинаково нужны, выбирается верхнее или нижнее. Порядок: еда, гигиена, сон, работа, отдых.",small);
            GUI.Label(new Rect(x+440,y+122,430,35),"УПРАВЛЕНИЕ",heading);
            string[] controls={"WASD — перемещение","Удерживать ПКМ — поворот камеры","Пробел — прыжок","Tab — MANUAL / AUTO","E — действие у мебели","Esc — пауза / назад","Отойти от мебели — отменить действие"};
            for(int i=0;i<controls.Length;i++)GUI.Label(new Rect(x+440,y+174+i*35,450,32),controls[i],small);
            if(Button(new Rect(x+(w-330)/2,y+h-84,330,48),"Назад"))BackFromSettings();
        }
        else
        {
            bool main=View==ScreenView.MainMenu;
            float buttonX=x+(w-380)/2;
            float buttonsHeight=main ? 204 : 279;
            float buttonsY=y+(h-buttonsHeight)/2+(main ? 20 : 35);
            if(Button(new Rect(buttonX,buttonsY,380,54),main ? "Начать игру" : "Продолжить")) {if(main)StartGame();else SetView(ScreenView.Game);}
            if(Button(new Rect(buttonX,buttonsY+75,380,54),"Настройки"))OpenSettings();
            if(!main && Button(new Rect(buttonX,buttonsY+150,380,54),"В главное меню"))ReturnToMainMenu();
            if(Button(new Rect(buttonX,buttonsY+(main?150:225),380,54),"Выход"))
            {
                Time.timeScale=1;AudioListener.pause=false;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying=false;
#else
                Application.Quit();
#endif
            }
        }
    }
    static string Translate(string action)
    {
        switch(action) {case "Eat":return "Поесть";case "Wash":return "Помыться";case "Sleep":return "Поспать";case "Work":return "Поработать";case "Relax":return "Отдохнуть";default:return action ?? "Ожидание";}
    }
}
