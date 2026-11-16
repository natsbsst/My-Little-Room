using UnityEngine;

[DefaultExecutionOrder(100)]
public class CityMenuBackdrop : MonoBehaviour
{
    Camera view;
    ThirdPersonMannequinCamera follow;
    LifeHUD hud;
    bool showingCity;
    void Start()
    {
        hud=GetComponent<LifeHUD>();
        view=Camera.main;
        if(view)follow=view.GetComponent<ThirdPersonMannequinCamera>();
    }
    void LateUpdate()
    {
        if(!view || !hud)return;
        if(hud.UsesCityBackdrop)
        {
            if(follow)follow.enabled=false;
            showingCity=true;
            // Exterior view of the apartment's existing modeled city, beyond the windows.
            view.transform.position=new Vector3(46f,4.8f,26f);
            view.transform.rotation=Quaternion.LookRotation(new Vector3(65f,-1f,27f)-view.transform.position);
        }
        else if(showingCity)
        {
            showingCity=false;
            if(follow) {follow.enabled=true;follow.Follow(transform);}
        }
    }
    void OnDisable() {if(follow)follow.enabled=true;}
}
