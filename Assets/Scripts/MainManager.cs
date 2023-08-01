using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using SimpleJSON;
using SocketIO;
using System.Net.Sockets;
using System.Net;

public class MainManager : MonoBehaviour
{
    WorkSceneType workscenetype = new WorkSceneType();
    bool[] sceneSoldoutMode = new bool[2];
    //splash
    public GameObject splashObj;
    public GameObject shop_open_popup;
    public GameObject decarbonate_popup;
    public GameObject splash_err_popup;
    public Text splash_err_title;
    public Text splash_err_content;
    public float delay_time = 0.5f;
    public float repeat_time = 5f;
    bool is_connection = false;

    //work
    public GameObject workObj;
    public GameObject pourImgObj;
    public GameObject remainImgObj;
    public GameObject[] wineObj;
    public GameObject[] wineBack;
    public GameObject[] soldoutObj;
    public GameObject[] priceObj;
    public GameObject wpriceObj;
    public Text[] contentObj;
    public Text[] noticeObj;
    public Text wcontentObj;
    public Text wnoticeObj;
    public GameObject socketPrefab;
    GameObject socketObj;
    SocketIOComponent socket;
    public GameObject err_popup;
    public Text err_title;
    public Text err_content;

    //setting
    public GameObject settingObj;
    public GameObject washPopup;
    public GameObject bottlePopup;
    public GameObject bottleInitPopup;
    public GameObject devicecheckingPopup;
    public GameObject set_savePopup;
    public GameObject set_errPopup;
    public Text set_errStr;

    //db_input
    public GameObject dbinputObj;

    public AudioSource[] soundObjs; //touch:0, alarm_soldout:1, alarm_max:2, alarm_remain:3, alarm_standby:4, alarm_beerchange:5 ,alarm_shopopen:6, alarm_shopclose:7, wash-8, keginit-9, alarm_start:10, alarm_prepay:11

    public float response_delay_time = 5f;
    int curWineIndex = 0;
    bool is_self = false;
    bool[] is_last = new bool[2] { false, false };
    bool shopFlag = false;
    bool standBTFlag = false;
    bool shopCloseHandType = false;

    //setting ui
    public InputField no;

    //db input ui
    public InputField[] dbInput = new InputField[2];//0-ip, 1-busid

    //setting->start
    bool is_set_finished = false;
    bool[] is_loaded = new bool[2] { false, false };

    void Awake()
    {
        Screen.orientation = ScreenOrientation.Landscape;
        Screen.fullScreen = true;
#if UNITY_ANDROID
        Global.setStatusBarValue(1024); // WindowManager.LayoutParams.FLAG_FORCE_NOT_FULLSCREEN
#endif
        soundObjs[10].Play();
    }

    // Start is called before the first frame update
    IEnumerator Start()
    {
#if UNITY_IPHONE
		Global.imgPath = Application.persistentDataPath + "/bself_wine2_img/";
#elif UNITY_ANDROID
        Global.imgPath = Application.persistentDataPath + "/bself_wine2_img/";
#else
if( Application.isEditor == true ){ 
    	Global.imgPath = "/img/";
} 
#endif

#if UNITY_IPHONE
		Global.prePath = @"file://";
#elif UNITY_ANDROID
        Global.prePath = @"file:///";
#else
		Global.prePath = @"file://" + Application.dataPath.Replace("/Assets","/");
#endif

        //delete all downloaded images
        try
        {
            if (Directory.Exists(Global.imgPath))
            {
                Directory.Delete(Global.imgPath, true);
            }
        }
        catch (Exception)
        {

        }
        LoadInfoFromPrefab();

        if (Global.pos_server_address == "" || Global.pos_server_address == null)
        {
            yield return new WaitForSeconds(delay_time);
            onShowScene(SceneStep.db_input);
        }
        else if (Global.setInfo[0].serial_number == 0 || Global.setInfo[1].serial_number == 0)
        {
            yield return new WaitForSeconds(delay_time);
            curWineIndex = 0;
            onShowScene(SceneStep.setting);
        }
        else
        {
            StartCoroutine(Connect());
        }
    }

    void LoadInfoFromPrefab()
    {
        Global.pos_server_address = PlayerPrefs.GetString("ip");
        Global.api_url = "http://" + Global.pos_server_address + ":" + Global.api_server_port + "/m-api/self/";
        Global.socket_server = "ws://" + Global.pos_server_address + ":" + Global.api_server_port;
        Global.image_server_path = "http://" + Global.pos_server_address + ":" + Global.api_server_port + "/self/";
        Global.posInfo.bus_id = PlayerPrefs.GetString("bus_id");
        Global.posInfo.appNo = PlayerPrefs.GetInt("appNo");
    }

    bool is_send_check_connect = false;
    IEnumerator Connect()
    {
        Debug.Log(is_set_finished);
        if (!is_set_finished)
        {
            while (true)
            {
                if (is_connection)
                {
                    onShowScene(SceneStep.work);
                    break;
                }
                if (!is_send_check_connect)
                {   
                    WWWForm form = new WWWForm();
                    form.AddField("bus_id", Global.posInfo.bus_id);
                    form.AddField("app_type", 1);//wine2
                    form.AddField("appNo", Global.posInfo.appNo);
                    WWW www = new WWW(Global.api_url + Global.check_db_api, form);
                    StartCoroutine(ProcessCheckConnect(www));
                    is_send_check_connect = true;
                }
                yield return new WaitForSeconds(delay_time);
            }
        }
        else
        {
            onShowScene(SceneStep.work);
        }
    }

    IEnumerator ProcessCheckConnect(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            Debug.Log(jsonNode);
            string result = jsonNode["suc"].ToString()/*.Replace("\"", "")*/;
            if (result == "1")
            {
                try
                {
                    if (jsonNode["is_self"].AsInt == 1)
                    {
                        is_self = true;
                    }
                    else
                    {
                        is_self = false;
                    }
                } catch(Exception ex)
                {
                    is_self = false;
                }

                JSONNode tapList = JSON.Parse(jsonNode["tapList"].ToString()/*.Replace("\"", "")*/);
                try
                {
                    for (int i = 0; i < tapList.Count; i++)
                    {
                        int serial_number = tapList[i]["serial_number"].AsInt;
                        int no = 0;
                        if (Global.setInfo[1].serial_number == serial_number)
                        {
                            no = 1;
                        }
                        Global.setInfo[no].max_limit = tapList[i]["max"].AsInt;
                        PlayerPrefs.SetInt("max1", Global.setInfo[no].max_limit);
                        Global.setInfo[no].sensor_spec = tapList[i]["sensor"].AsInt;
                        PlayerPrefs.SetInt("sensor" + no, Global.setInfo[no].sensor_spec);
                        Global.setInfo[no].open_time = tapList[i]["opentime"].AsInt;
                        PlayerPrefs.SetInt("opentime" + no, Global.setInfo[no].open_time);
                        Global.setInfo[no].sell_type = tapList[i]["sell_type"].AsInt;
                        PlayerPrefs.SetInt("selltype" + no, Global.setInfo[no].sell_type);
                        Global.setInfo[no].decarbo_time = tapList[i]["decarbo_time"].AsInt;
                        PlayerPrefs.SetInt("decarbotime" + no, Global.setInfo[no].decarbo_time);
                        Global.setInfo[no].board_no = tapList[i]["board_no"].AsInt;
                        PlayerPrefs.SetInt("board_no" + no, Global.setInfo[no].board_no);
                        Global.setInfo[no].board_channel = tapList[i]["board_channel"].AsInt;
                        PlayerPrefs.SetInt("board_channel" + no, Global.setInfo[no].board_channel);
                        Global.setInfo[no].tagGW_no = tapList[i]["gw_no"].AsInt;
                        PlayerPrefs.SetInt("taggw_no" + no, Global.setInfo[no].tagGW_no);
                        Global.setInfo[no].tagGW_channel = tapList[i]["gw_channel"].AsInt;
                        PlayerPrefs.SetInt("taggw_channel" + no, Global.setInfo[no].tagGW_channel);
                        is_loaded[no] = false;
                        if (Global.setInfo[no].tagGW_no != 0 && Global.setInfo[no].tagGW_channel != 0 && Global.setInfo[no].board_no != 0 && Global.setInfo[no].board_channel != 0)
                        {
                            is_loaded[no] = true;
                        }
                        DownloadWorkImg(true);
                    }
                } catch(Exception ex)
                {

                }
                StopCoroutine("Connect");
                yield return new WaitForSeconds(delay_time);
                splash_err_popup.SetActive(false);
                if (socketObj == null)
                {
                    socketObj = Instantiate(socketPrefab);
                    socket = socketObj.GetComponent<SocketIOComponent>();
                    socket.On("open", socketOpen);
                    socket.On("LoadDeivceInfo", LoadDeviceInfo);
                    socket.On("shopOpen", OpenShopEventHandler);
                    socket.On("shopClose", CloseShopEventHandler);
                    socket.On("soldoutOccured", SoldoutEventHandler);
                    socket.On("RepairingDevice", RepairingDevice);
                    socket.On("changeProductInfo", ChangeWineInfo);
                    socket.On("changeSetInfo", ChangeSetInfo);
                    socket.On("selftagVerifyResponse", TagVerifyResponse);
                    socket.On("startResponse", startResponse);
                    socket.On("flowmeterValue", FlowmeterValueEventHandler);
                    socket.On("flowmeterFinish", FlowmeterFinishEventHandler);
                    socket.On("boardconnectionFailed", boardconnectionFailed);
                    socket.On("gwconnectionFailed", boardconnectionFailed);
                    socket.On("ConnectFailInfo", ConnectFailInfo);
                    socket.On("FinishFailInfo", ConnectFailInfo);
                    socket.On("error", socketError);
                    socket.On("close", socketClose);
                }
                StopCoroutine("checkIsSelf");
                StartCoroutine(checkIsSelf());
                is_set_finished = true;
                for(int i = 0; i < is_loaded.Length; i ++)
                {
                    if(!is_loaded[i])
                    {
                        is_set_finished = false;
                    }
                }
                is_connection = true;
            }
            else
            {
                splash_err_title.text = "Connecting to POS";
                splash_err_content.text = "서버와 연결 중입니다. 잠시만 기다려주세요.";
                splash_err_popup.SetActive(true);
            }
        }
        else
        {
            is_send_check_connect = false;
            splash_err_title.text = "Connecting to POS";
            splash_err_content.text = "서버와 연결 중입니다. 잠시만 기다려주세요.";
            splash_err_popup.SetActive(true);
        }
    }

    public void boardconnectionFailed(SocketIOEvent e)
    {
        Debug.Log("tcp disconnection event.");
        JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
        int status = jsonNode["status"].AsInt;
        if (status == 1)
        {
            err_popup.SetActive(false);
        }
        else
        {
            err_title.text = "Connecting to Server";
            err_content.text = "서버와 연결 중입니다. 잠시만 기다려주세요.";
            err_popup.SetActive(true);
        }
    }

    public void ConnectFailInfo(SocketIOEvent e)
    {
        Debug.Log("socket connect failed event.");
        err_title.text = "Connecting to Server";
        err_content.text = "서버와 연결 중입니다. 잠시만 기다려주세요.";
        err_popup.SetActive(true);
        workscenetype = WorkSceneType.standby;
        onShowScene(SceneStep.work);
        LoadInfo();
    }

    IEnumerator checkIsSelf()
    {
        while (true)
        {
            yield return new WaitForSeconds(6 * 3600);
            WWWForm form = new WWWForm();
            form.AddField("bus_id", Global.posInfo.bus_id);
            form.AddField("app_type", 1);//wine2
            form.AddField("appNo", Global.posInfo.appNo);
            WWW www = new WWW(Global.api_url + Global.check_db_api, form);
            StartCoroutine(ProcessCheckIsSelf(www));
        }
    }

    IEnumerator ProcessCheckIsSelf(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            string result = jsonNode["suc"].ToString()/*.Replace("\"", "")*/;
            if (result == "1")
            {
                try
                {
                    if (jsonNode["is_self"].AsInt == 1)
                    {
                        is_self = true;
                    }
                    else
                    {
                        is_self = false;
                    }
                }
                catch (Exception ex)
                {
                    is_self = false;
                }
            }
            else
            {
                is_self = false;
            }
        }
        else
        {
            is_self = false;
        }
    }

    void onShowScene(SceneStep scene_step)
    {
        if (scene_step != SceneStep.setting)
        {
            splashObj.SetActive(false);
            workObj.SetActive(false);
            settingObj.SetActive(false);
            dbinputObj.SetActive(false);
        }
        switch (scene_step)
        {
            case SceneStep.splash://splash
                {
                    splashObj.SetActive(true);
                    break;
                };
            case SceneStep.db_input://db input
                {
                    dbinputObj.SetActive(true);
                    dbInput[0].text = Global.pos_server_address;
                    break;
                };
            case SceneStep.setting://setting
                {
                    splashObj.SetActive(false);
                    workObj.SetActive(false);
                    settingObj.SetActive(false);
                    dbinputObj.SetActive(false);
                    settingObj.SetActive(true);
                    try
                    {
                        no.text = Global.setInfo[curWineIndex].serial_number.ToString();
                    }
                    catch (Exception ex)
                    {
                        Debug.Log(ex);
                    }
                    break;
                };
            case SceneStep.work://work
                {
                    StartCoroutine(ProcessGetProductInfo());
                    workObj.SetActive(true);
                    break;
                };
        }
    }

    void onShowWorkSceneWithoutGetProduct()
    {
        splashObj.SetActive(false);
        settingObj.SetActive(false);
        dbinputObj.SetActive(false);
        workObj.SetActive(true);
    }

    IEnumerator ProcessGetProductInfo()
    {
        while (true)
        {
            WWWForm form = new WWWForm();
            form.AddField("appNo", Global.posInfo.appNo);
            WWW www = new WWW(Global.api_url + Global.get_product_api, form);
            yield return www;
            if (www.error == null)
            {
                JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
                string result = jsonNode["suc"].ToString()/*.Replace("\"", "")*/;
                if (result == "1")
                {
                    try
                    {
                        JSONNode wineInfos = JSON.Parse(jsonNode["wineList"].ToString());
                        for (int i = 0; i < wineInfos.Count; i++)
                        {
                            if (i >= 2) break;
                            int selectedIndex = -1;
                            for (int j = 0; j < Global.setInfo.Length; j++)
                            {
                                if (Global.setInfo[j].serial_number == wineInfos[i]["serial_number"].AsInt)
                                {
                                    selectedIndex = j;
                                    break;
                                }
                            }
                            if (selectedIndex != -1)
                            {
                                Global.setInfo[selectedIndex].wineInfo.wine_id = wineInfos[i]["beer_id"];
                                DownloadWorkImg(false);
                                wineObj[selectedIndex].SetActive(true);
                                Global.setInfo[selectedIndex].wineInfo.server_id = wineInfos[i]["server_id"].AsInt;
                                Global.setInfo[selectedIndex].wineInfo.total_amount = wineInfos[i]["total_amount"].AsInt;
                                Global.setInfo[selectedIndex].wineInfo.ml_unit_price = wineInfos[i]["unit_price"].AsInt;
                                Global.setInfo[selectedIndex].wineInfo.cup_unit_price = wineInfos[i]["cup_unit_price"].AsInt;
                                Global.setInfo[selectedIndex].tagGW_no = wineInfos[i]["gw_no"].AsInt;
                                Global.setInfo[selectedIndex].tagGW_channel = wineInfos[i]["gw_channel"].AsInt;
                                Global.setInfo[selectedIndex].board_no = wineInfos[i]["board_no"].AsInt;
                                Global.setInfo[selectedIndex].board_channel = wineInfos[i]["board_channel"].AsInt;
                                is_loaded[selectedIndex] = false;
                                if (Global.setInfo[selectedIndex].tagGW_no != 0 && Global.setInfo[selectedIndex].tagGW_channel != 0 && Global.setInfo[selectedIndex].board_no != 0 && Global.setInfo[selectedIndex].board_channel != 0)
                                {
                                    is_loaded[selectedIndex] = true;
                                    PlayerPrefs.SetInt("taggw_no" + selectedIndex, Global.setInfo[selectedIndex].tagGW_no);
                                    PlayerPrefs.SetInt("taggw_channel" + selectedIndex, Global.setInfo[selectedIndex].tagGW_channel);
                                    PlayerPrefs.SetInt("board_no" + selectedIndex, Global.setInfo[selectedIndex].board_no);
                                    PlayerPrefs.SetInt("board_channel" + selectedIndex, Global.setInfo[selectedIndex].board_channel);
                                }
                                if (wineInfos[i]["sold_out"].AsInt == 1)
                                {
                                    Global.setInfo[selectedIndex].wineInfo.is_soldout = true;
                                    sceneSoldoutMode[selectedIndex] = true;
                                    curWineIndex = selectedIndex;
                                    LoadInfo();
                                }
                                else
                                {
                                    Global.setInfo[selectedIndex].wineInfo.is_soldout = false;
                                    sceneSoldoutMode[selectedIndex] = false;
                                    curWineIndex = selectedIndex;
                                    onShowScene(SceneStep.work);
                                    LoadInfo();
                                }
                            }
                        }
                    } catch(Exception ex)
                    {
                        Debug.Log(ex);
                    }
                }
                else
                {
                    for (int i = 0; i < 2; i++)
                        wineObj[i].SetActive(false);
                }
                break;
            }
            else
            {
                err_title.text = "No Wine";
                err_content.text = "와인정보를 확인하세요.";
                err_popup.SetActive(true);
                yield return new WaitForSeconds(repeat_time);
            }
        }
    }

    public void onDBInput()
    {
        onShowScene(SceneStep.db_input);
    }

    public void onBack()
    {
        if (dbinputObj.activeSelf)
        {
            onShowScene(SceneStep.setting);
        }
        else if (settingObj.activeSelf)
        {
            if (Global.pos_server_address == "" || Global.pos_server_address == null)
            {
                onShowScene(SceneStep.db_input);
            }
            else
            {
                bool is_setted = true;
                for (int i = 0; i < Global.setInfo.Length; i++)
                {
                    if (Global.setInfo[i].max_limit == 0)
                    {
                        is_setted = false;
                        set_errStr.text = "설정값들을 저장하세요.";
                        set_errPopup.SetActive(true);
                        break;
                    }
                }
                if (is_setted)
                {
                    StartCoroutine(Connect());
                }
            }
        }
        else
        {
            string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo[curWineIndex].tagGW_no + "\"," +
            "\"ch_value\":\"" + Global.setInfo[curWineIndex].tagGW_channel + "\"," +
            "\"status\":\"" + 1 + "\"}";
            socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));
            onShowScene(SceneStep.work);
        }
    }

    public void onDecarbonate()
    {
        StartCoroutine(Decarbonate());
    }

    IEnumerator ShowErrPopup()
    {
        err_popup.SetActive(true);
        yield return new WaitForSeconds(3f);
        err_popup.SetActive(false);
        workscenetype = WorkSceneType.standby;
        onShowScene(SceneStep.work);
        LoadInfo();
    }

    public void startResponse(SocketIOEvent e)
    {
        try
        {
            open_tagResponse_time = DateTime.Now;
            open_tagResponse_timeA = open_tagResponse_time.AddMinutes(Global.setInfo[curWineIndex].standby_time);
            workscenetype = WorkSceneType.pour;
            LoadInfo();
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
    }

    public void TagVerifyResponse(SocketIOEvent e)
    {
        try
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            Debug.Log(jsonNode);
            if (Global.setInfo[curWineIndex].wineInfo.wine_id == "" || Global.setInfo[curWineIndex].wineInfo.wine_id == null)
            {
                return;
            }
            if (jsonNode["is_manage_card"].AsInt == 1)
            {
                onShowScene(SceneStep.setting);
            }
            else
            {
                onShowScene(SceneStep.work);
                if (!sceneSoldoutMode[curWineIndex] && is_self)
                {
                    if (jsonNode["suc"].AsInt == 1)
                    {
                        workscenetype = WorkSceneType.standby;
                        LoadInfo();
                        int status = jsonNode["status"].AsInt;
                        switch (status)
                        {
                            case 0:
                                {
                                    err_title.text = "This Tag is in use";
                                    err_content.text = "태그가 사용 중입니다.\n잠시 후에 사용해 주세요.";
                                    StartCoroutine(ShowErrPopup());
                                    break;
                                };
                            case 1:
                                {
                                    JSONNode tagData = JSON.Parse(jsonNode["tag"].ToString()/*.Replace("\"", "")*/);
                                    Global.setInfo[curWineIndex].tagInfo = new TagInfo();
                                    Global.setInfo[curWineIndex].tagInfo.use_amt = tagData["use_amt"].AsInt;
                                    Global.setInfo[curWineIndex].tagInfo.prepay_amt = tagData["prepay_amt"].AsInt;
                                    Global.setInfo[curWineIndex].tagInfo.is_pay_after = tagData["is_pay_after"].AsInt;
                                    is_last[curWineIndex] = false;
                                    break;
                                };
                            case 2:
                                {
                                    err_title.text = "Unregistered Tag";
                                    err_content.text = "등록되지 않은 태그입니다.";
                                    StartCoroutine(ShowErrPopup());
                                    break;
                                };
                            case 3:
                                {
                                    err_title.text = "Lost Tag";
                                    err_content.text = "분실된 TAG입니다.\n카운터에 반납해주세요.";
                                    StartCoroutine(ShowErrPopup());
                                    break;
                                };
                            case 4:
                                {
                                    err_title.text = "Recharge your Tag";
                                    err_content.text = "남은 금액이 없습니다.\n충전 후에 사용하세요.";
                                    StartCoroutine(ShowErrPopup());
                                    break;
                                };
                            case 5:
                                {
                                    err_title.text = "Recharge your Tag";
                                    err_content.text = "남은 금액이 부족합니다.\n충전 후에 사용하세요.";
                                    StartCoroutine(ShowErrPopup());
                                    break;
                                };
                            case 6:
                                {
                                    err_title.text = "Expired Tag";
                                    err_content.text = "사용기한이 지난 태그입니다.";
                                    StartCoroutine(ShowErrPopup());
                                    break;
                                };
                        }
                    }
                    else
                    {
                        err_title.text = "This Tag is in use";
                        err_content.text = "태그가 사용 중입니다.\n잠시 후에 사용해 주세요.";
                        StartCoroutine(ShowErrPopup());
                    }
                }
            }
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    IEnumerator Decarbonate()
    {
        Debug.Log("curWineIndex : " + curWineIndex);
        if (socket != null) {
            string data = "{\"tagGW_no\":\"" + Global.setInfo[curWineIndex].tagGW_no + "\"," +
            "\"ch_value\":\"" + 1 + "\"," +
            "\"status\":\"" + 1 + "\"}";
            socket.Emit("deviceTagLock", JSONObject.Create(data));
        }
        yield return new WaitForSeconds(Global.setInfo[curWineIndex].decarbo_time);
        if (!shopCloseHandType && socket != null)
        {
            string data = "{\"tagGW_no\":\"" + Global.setInfo[curWineIndex].tagGW_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo[curWineIndex].tagGW_channel + "\"," +
                "\"status\":\"" + 1 + "\"}";
            socket.Emit("deviceTagLock", JSONObject.Create(data));

            data = "{\"board_no\":\"" + Global.setInfo[curWineIndex].board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo[curWineIndex].board_channel + "\"," +
                "\"valve\":\"" + 1 + "\"," +
                "\"status\":\"" + 0 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));
            decarbonate_popup.SetActive(false);
            workscenetype = WorkSceneType.standby;
            onShowScene(SceneStep.work);
            LoadInfo();
        }
    }

    public void SaveSetInfo()
    {
        if (no.text == "")
        {
            set_errStr.text = "기기번호를 입력하세요.";
            set_errPopup.SetActive(true);
        }
        else
        {
            SettingInfo sinfo = new SettingInfo();
            sinfo.serial_number = int.Parse(no.text);
            int old_serial_number = 0;
            if(is_set_finished)
            {
                old_serial_number = Global.setInfo[curWineIndex].serial_number;
                Global.setInfo[0].open_time = sinfo.open_time;
                Global.setInfo[1].open_time = sinfo.open_time;
                Global.setInfo[curWineIndex] = sinfo;
                PlayerPrefs.SetInt("serial_number" + curWineIndex, Global.setInfo[curWineIndex].serial_number);
                PlayerPrefs.SetInt("max" + curWineIndex, Global.setInfo[curWineIndex].max_limit);
                PlayerPrefs.SetInt("sensor" + curWineIndex, Global.setInfo[curWineIndex].sensor_spec);
                PlayerPrefs.SetInt("opentime" + curWineIndex, Global.setInfo[curWineIndex].open_time);
                PlayerPrefs.SetInt("decarbotime" + curWineIndex, Global.setInfo[curWineIndex].decarbo_time);
            }
            else
            {
                Global.setInfo[0] = sinfo;
                sinfo.serial_number = sinfo.serial_number + 1;
                Global.setInfo[1] = sinfo;
                PlayerPrefs.SetInt("serial_number0", Global.setInfo[0].serial_number);
                PlayerPrefs.SetInt("max0", Global.setInfo[0].max_limit);
                PlayerPrefs.SetInt("sensor0", Global.setInfo[0].sensor_spec);
                PlayerPrefs.SetInt("opentime0", Global.setInfo[0].open_time);
                PlayerPrefs.SetInt("decarbotime0", Global.setInfo[0].decarbo_time);
                PlayerPrefs.SetInt("serial_number1", Global.setInfo[1].serial_number);
                PlayerPrefs.SetInt("max1", Global.setInfo[1].max_limit);
                PlayerPrefs.SetInt("sensor1", Global.setInfo[1].sensor_spec);
                PlayerPrefs.SetInt("opentime1", Global.setInfo[1].open_time);
                PlayerPrefs.SetInt("decarbotime1", Global.setInfo[1].decarbo_time);
            }

            WWWForm form = new WWWForm();
            form.AddField("app_type", 1);
            form.AddField("appNo", Global.posInfo.appNo);
            form.AddField("old_serial_number", old_serial_number);
            form.AddField("set_type", is_set_finished ? 1 : 0);//0 : set both, 1 : set one
            form.AddField("serial_number", Global.setInfo[curWineIndex].serial_number);
            form.AddField("max_limit", Global.setInfo[curWineIndex].max_limit);
            form.AddField("sensor_spec", Global.setInfo[curWineIndex].sensor_spec);
            form.AddField("open_setting_time", Global.setInfo[curWineIndex].open_time);
            WWW www = new WWW(Global.api_url + Global.save_tapinfo_api, form);
            StartCoroutine(SaveTapInfo(www));
        }
    }

    IEnumerator SaveTapInfo(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            int result = jsonNode["suc"].AsInt;
            if (result == 1)
            {
                try
                {
                    Global.posInfo.appNo = jsonNode["appNo"].AsInt;
                    PlayerPrefs.SetInt("appNo", Global.posInfo.appNo);
                    if(is_set_finished)
                    {
                        Global.setInfo[curWineIndex].tagGW_no = jsonNode["tagGW_no"].AsInt;
                        Global.setInfo[curWineIndex].tagGW_channel = jsonNode["tagGW_channel"].AsInt;
                        Global.setInfo[curWineIndex].board_no = jsonNode["board_no"].AsInt;
                        Global.setInfo[curWineIndex].board_channel = jsonNode["board_channel"].AsInt;

                        is_loaded[curWineIndex] = false;
                        if (Global.setInfo[curWineIndex].tagGW_no != 0 && Global.setInfo[curWineIndex].tagGW_channel != 0 && Global.setInfo[curWineIndex].board_no != 0 && Global.setInfo[curWineIndex].board_channel != 0)
                        {
                            is_loaded[curWineIndex] = true;
                            PlayerPrefs.SetInt("taggw_no" + curWineIndex, Global.setInfo[curWineIndex].tagGW_no);
                            PlayerPrefs.SetInt("taggw_channel" + curWineIndex, Global.setInfo[curWineIndex].tagGW_channel);
                            PlayerPrefs.SetInt("board_no" + curWineIndex, Global.setInfo[curWineIndex].board_no);
                            PlayerPrefs.SetInt("board_channel" + curWineIndex, Global.setInfo[curWineIndex].board_channel);
                        }
                    }
                    else
                    {
                        Global.setInfo[0].tagGW_no = jsonNode["tagGW_no"].AsInt;
                        Global.setInfo[0].tagGW_channel = jsonNode["tagGW_channel"].AsInt;
                        Global.setInfo[0].board_no = jsonNode["board_no"].AsInt;
                        Global.setInfo[0].board_channel = jsonNode["board_channel"].AsInt;

                        is_loaded[0] = false;
                        if (Global.setInfo[0].tagGW_no != 0 && Global.setInfo[0].tagGW_channel != 0 && Global.setInfo[0].board_no != 0 && Global.setInfo[0].board_channel != 0)
                        {
                            is_loaded[0] = true;
                            PlayerPrefs.SetInt("taggw_no0", Global.setInfo[0].tagGW_no);
                            PlayerPrefs.SetInt("taggw_channel0", Global.setInfo[0].tagGW_channel);
                            PlayerPrefs.SetInt("board_no0", Global.setInfo[0].board_no);
                            PlayerPrefs.SetInt("board_channel0", Global.setInfo[0].board_channel);
                        }

                        Global.setInfo[1].tagGW_no = jsonNode["tagGW_no1"].AsInt;
                        Global.setInfo[1].tagGW_channel = jsonNode["tagGW_channel1"].AsInt;
                        Global.setInfo[1].board_no = jsonNode["board_no1"].AsInt;
                        Global.setInfo[1].board_channel = jsonNode["board_channel1"].AsInt;

                        is_loaded[1] = false;
                        if (Global.setInfo[1].tagGW_no != 0 && Global.setInfo[1].tagGW_channel != 0 && Global.setInfo[1].board_no != 0 && Global.setInfo[1].board_channel != 0)
                        {
                            is_loaded[1] = true;
                            PlayerPrefs.SetInt("taggw_no1", Global.setInfo[1].tagGW_no);
                            PlayerPrefs.SetInt("taggw_channel1", Global.setInfo[1].tagGW_channel);
                            PlayerPrefs.SetInt("board_no1", Global.setInfo[1].board_no);
                            PlayerPrefs.SetInt("board_channel1", Global.setInfo[1].board_channel);
                        }
                    }
                    set_savePopup.SetActive(true);
                    is_set_finished = true;
                    for(int i = 0; i < is_loaded.Length; i ++)
                    {
                        if(!is_loaded[i])
                        {
                            is_set_finished = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log(ex);
                }
            }
            else
            {
                set_errStr.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
                set_errPopup.SetActive(true);
            }
        }
        else
        {
            set_errStr.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
            set_errPopup.SetActive(true);
        }
    }

    public void onConfirmErrPopup()
    {
        set_errStr.text = "";
        set_errPopup.SetActive(false);
    }

    public void onConfirmSavePopup()
    {
        set_savePopup.SetActive(false);
    }

    public void Wash()
    {
        if (!is_loaded[curWineIndex])
        {
            set_errStr.text = "제어보드 셋팅을 진행하세요.";
            set_errPopup.SetActive(true);
        }
        else if (Global.setInfo[curWineIndex].wineInfo.wine_id == "" || Global.setInfo[curWineIndex].wineInfo.wine_id == null)
        {
            set_errStr.text = "와인정보가 없습니다.";
            set_errPopup.SetActive(true);
        }
        else
        {
            soundObjs[8].Play();
            washPopup.SetActive(true);
            string data = "{\"board_no\":\"" + Global.setInfo[curWineIndex].board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo[curWineIndex].board_channel + "\"," +
                "\"valve\":\"" + 0 + "\"," +
                "\"status\":\"" + 1 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));
        }
    }

    public void onConfirmWashPopup()
    {
        washPopup.SetActive(false);
        onShowWorkSceneWithoutGetProduct();
        workscenetype = WorkSceneType.standby;
        onShowScene(SceneStep.work);
        LoadInfo();

        string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo[curWineIndex].tagGW_no + "\"," +
            "\"ch_value\":\"" + Global.setInfo[curWineIndex].tagGW_channel + "\"," +
            "\"status\":\"" + 1 + "\"}";
        socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));

        string data = "{\"board_no\":\"" + Global.setInfo[curWineIndex].board_no + "\"," +
            "\"ch_value\":\"" + Global.setInfo[curWineIndex].board_channel + "\"," +
            "\"valve\":\"" + 0 + "\"," +
            "\"status\":\"" + 0 + "\"}";
        socket.Emit("boardValveCtrl", JSONObject.Create(data));
    }

    public void BottleChange()
    {
        if (!is_loaded[curWineIndex])
        {
            set_errStr.text = "제어보드 셋팅을 진행하세요.";
            set_errPopup.SetActive(true);
        }
        else if (Global.setInfo[curWineIndex].wineInfo.wine_id == "" || Global.setInfo[curWineIndex].wineInfo.wine_id == null)
        {
            set_errStr.text = "와인정보가 없습니다.";
            set_errPopup.SetActive(true);
        }
        else
        {
            bottlePopup.SetActive(true);
            string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo[curWineIndex].tagGW_no + "\"," +
            "\"ch_value\":\"" + Global.setInfo[curWineIndex].tagGW_channel + "\"," +
            "\"status\":\"" + 0 + "\"}";
            socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));

            string data = "{\"board_no\":\"" + Global.setInfo[curWineIndex].board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo[curWineIndex].board_channel + "\"," +
                "\"valve\":\"" + 0 + "\"," +
                "\"status\":\"" + 1 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));
        }
    }

    public void onConfirmBottlePopup()
    {
        bottlePopup.SetActive(false);
        bottleInitPopup.SetActive(true);
    }

    IEnumerator ProcessBottleInitConfirmApi()
    {
        WWWForm form = new WWWForm();
        form.AddField("serial_number", Global.setInfo[curWineIndex].serial_number);
        form.AddField("total_amount", Global.setInfo[curWineIndex].wineInfo.total_amount);
        WWW www = new WWW(Global.api_url + Global.bottle_init_confirm_api, form);
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            string result = jsonNode["suc"].ToString()/*.Replace("\"", "")*/;
            if (result == "1")
            {
                soundObjs[9].Play();
                onShowWorkSceneWithoutGetProduct();
                onShowScene(SceneStep.work);
                workscenetype = WorkSceneType.standby;
                LoadInfo();
                bottleInitPopup.SetActive(false);
                err_popup.SetActive(false);
                if (socket != null)
                {
                    string data = "{\"board_no\":\"" + Global.setInfo[curWineIndex].board_no + "\"," +
                        "\"ch_value\":\"" + Global.setInfo[curWineIndex].board_channel + "\"," +
                        "\"valve\":\"" + 0 + "\"," +
                        "\"status\":\"" + 0 + "\"}";
                    socket.Emit("boardValveCtrl", JSONObject.Create(data));

                    data = "{\"tagGW_no\":\"" + Global.setInfo[curWineIndex].tagGW_no + "\"," +
                        "\"ch_value\":\"" + Global.setInfo[curWineIndex].tagGW_channel + "\"," +
                        "\"status\":\"" + 1 + "\"}";
                    socket.Emit("deviceTagLock", JSONObject.Create(data));
                }
            }
        }
        else
        {
            err_title.text = "Connecting to POS";
            err_content.text = "서버와 연결 중입니다. 잠시만 기다려주세요.";
            err_popup.SetActive(true);
        }
    }

    public void onConfirmBottleInitPopup()
    {
        if(socket != null)
        {
            string data = "{\"board_no\":\"" + Global.setInfo[curWineIndex].board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo[curWineIndex].board_channel + "\"," +
                "\"valve\":\"" + 0 + "\"," +
                "\"status\":\"" + 1 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));
            StartCoroutine(ProcessBottleInitConfirmApi());
        }
    }

    public void onCancelBottleInitPopup()
    {
        if(socket != null)
        {
            string data = "{\"board_no\":\"" + Global.setInfo[curWineIndex].board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo[curWineIndex].board_channel + "\"," +
                "\"valve\":\"" + 0 + "\"," +
                "\"status\":\"" + 0 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));
        }
        bottleInitPopup.SetActive(false);
        onShowScene(SceneStep.setting);
    }

    public void Soldout()
    {
        if (!is_loaded[curWineIndex])
        {
            set_errStr.text = "제어보드 셋팅을 진행하세요.";
            set_errPopup.SetActive(true);
        }
        else if (Global.setInfo[curWineIndex].wineInfo.wine_id == "" || Global.setInfo[curWineIndex].wineInfo.wine_id == null)
        {
            set_errStr.text = "와인정보가 없습니다.";
            set_errPopup.SetActive(true);
        }
        else
        {
            WWWForm form = new WWWForm();
            form.AddField("serial_number", Global.setInfo[curWineIndex].serial_number);
            WWW www = new WWW(Global.api_url + Global.soldout_api, form);
            StartCoroutine(SoldoutProcess(www));
        }
    }

    IEnumerator SoldoutProcess(WWW www)
    {
        yield return www;
        if(www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            int result = jsonNode["suc"].AsInt;
            if(result == 1)
            {
                soundObjs[1].Play();
                onShowWorkSceneWithoutGetProduct();
                sceneSoldoutMode[curWineIndex] = true;
                Global.setInfo[curWineIndex].wineInfo.is_soldout = true;
                LoadInfo();
                err_content.text = "";
                err_title.text = "";
                err_popup.SetActive(false);
            }
            else
            {
                set_errStr.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
                set_errPopup.SetActive(true);
            }
        }
        else
        {
            set_errStr.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
            set_errPopup.SetActive(true);
        }
    }

    public void DBInfoSave()
    {
        if (dbInput[0].text == "" || dbInput[0].text == null)
        {
            set_errStr.text = "IP를 입력하세요.";
            set_errPopup.SetActive(true);
        }
        else if(dbInput[1].text == "" || dbInput[1].text == null)
        {
            set_errStr.text = "사업자번호를 입력하세요.";
            set_errPopup.SetActive(true);
        }
        else
        {
            Global.pos_server_address = dbInput[0].text;
            Global.posInfo.bus_id = dbInput[1].text;
            Global.api_url = "http://" + Global.pos_server_address + ":" + Global.api_server_port + "/m-api/self/";
            Global.socket_server = "ws://" + Global.pos_server_address + ":" + Global.api_server_port;
            Global.image_server_path = "http://" + Global.pos_server_address + ":" + Global.api_server_port + "/self/";
            WWWForm form = new WWWForm();
            form.AddField("bus_id", Global.posInfo.bus_id);
            form.AddField("app_type", 1);//wine2
            form.AddField("appNo", Global.posInfo.appNo);
            WWW www = new WWW(Global.api_url + Global.check_db_api, form);
            StartCoroutine(ProcessCheckConnect1(www));
        }
    }

    IEnumerator ProcessCheckConnect1(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            string result = jsonNode["suc"].ToString()/*.Replace("\"", "")*/;
            if (result == "1")
            {
                try
                {
                    if (jsonNode["is_self"].AsInt == 1)
                    {
                        is_self = true;
                    }
                    else
                    {
                        is_self = false;
                    }
                }
                catch (Exception ex)
                {
                    is_self = false;
                }
                Global.posInfo = new POS_Info();
                Global.pos_server_address = dbInput[0].text;
                Global.posInfo.bus_id = dbInput[1].text;
                Global.api_url = "http://" + Global.pos_server_address + ":" + Global.api_server_port + "/m-api/self/";
                Global.socket_server = "ws://" + Global.pos_server_address + ":" + Global.api_server_port;
                Global.image_server_path = "http://" + Global.pos_server_address + ":" + Global.api_server_port + "/self/";
                //if (backgroundDefault.isOn)
                //{
                //    Global.posInfo.backgroundType = false;
                //    PlayerPrefs.SetInt("backgroundType", 0);
                //}
                //else
                //{
                //    Global.posInfo.backgroundType = true;
                //    PlayerPrefs.SetInt("backgroundType", 1);
                //}
                //if (wineInfoPos.isOn)
                //{
                //    Global.posInfo.wineInfoType = false;
                //    PlayerPrefs.SetInt("wineInfoType", 0);
                //}
                //else
                //{
                //    Global.posInfo.wineInfoType = true;
                //    PlayerPrefs.SetInt("wineInfoType", 1);
                //}
                PlayerPrefs.SetString("ip", Global.pos_server_address);
                PlayerPrefs.SetString("bus_id", Global.posInfo.bus_id);
                Global.posInfo.appNo = jsonNode["appNo"].AsInt;
                PlayerPrefs.SetInt("appNo", Global.posInfo.appNo);
                if (socket != null)
                {
                    socket.Close();
                    socket.OnDestroy();
                    socket.OnApplicationQuit();
                }
                if (socketObj != null)
                {
                    DestroyImmediate(socketObj);
                }
                socketObj = Instantiate(socketPrefab);
                socket = socketObj.GetComponent<SocketIOComponent>();
                socket.On("open", socketOpen);
                socket.On("LoadDeivceInfo", LoadDeviceInfo);
                socket.On("shopOpen", OpenShopEventHandler);
                socket.On("shopClose", CloseShopEventHandler);
                socket.On("soldoutOccured", SoldoutEventHandler);
                socket.On("RepairingDevice", RepairingDevice);
                socket.On("changeProductInfo", ChangeWineInfo);
                socket.On("tagVerifyResponse", TagVerifyResponse);
                socket.On("startResponse", startResponse);
                socket.On("changeSetInfo", ChangeSetInfo);
                socket.On("flowmeterValue", FlowmeterValueEventHandler);
                socket.On("flowmeterFinish", FlowmeterFinishEventHandler);
                socket.On("ConnectFailInfo", ConnectFailInfo);
                socket.On("FinishFailInfo", ConnectFailInfo);
                socket.On("boardconnectionFailed", boardconnectionFailed);
                socket.On("gwconnectionFailed", boardconnectionFailed);
                socket.On("error", socketError);
                socket.On("close", socketClose);
                set_savePopup.SetActive(true);
                StopCoroutine("checkIsSelf");
                StartCoroutine(checkIsSelf());
            }
            else
            {
                set_errStr.text = "디비정보를 확인하세요.";
                set_errPopup.SetActive(true);
            }
        }
        else
        {
            set_errStr.text = "디비정보를 확인하세요.";
            set_errPopup.SetActive(true);
        }
    }

    void DownloadWorkImg(bool init = false)
    {
        if (init)
        {
            remainImgObj.SetActive(true);
            string remain_url = Global.image_server_path + "Remain.jpg";
            StartCoroutine(downloadImage(remain_url, Global.imgPath + Path.GetFileName(remain_url), remainImgObj));
        }
        else
        {
            if (Global.setInfo[0].wineInfo.wine_id == "" || Global.setInfo[0].wineInfo.wine_id == null
                || Global.setInfo[1].wineInfo.wine_id == "" || Global.setInfo[1].wineInfo.wine_id == null)
            {

            }
            else
            {
                wineObj[0].SetActive(true);
                wineBack[0].SetActive(true);
                wineObj[1].SetActive(true);
                wineBack[1].SetActive(true);
                string url = Global.image_server_path + "Standby" + Global.setInfo[0].wineInfo.server_id + ".jpg";
                StartCoroutine(downloadImage(url, Global.imgPath + Path.GetFileName(url), wineBack[0]));
                url = Global.image_server_path + "Standby" + Global.setInfo[1].wineInfo.server_id + ".jpg";
                StartCoroutine(downloadImage(url, Global.imgPath + Path.GetFileName(url), wineBack[1]));
                pourImgObj.SetActive(true);
                url = Global.image_server_path + "Pour" + Global.setInfo[0].wineInfo.server_id + ".jpg";
                StartCoroutine(downloadImage(url, Global.imgPath + Path.GetFileName(url), pourImgObj));
            }
        }
        LoadWorkImg();
    }

    void LoadWorkImg()
    {
        if(workscenetype == WorkSceneType.remain)
        {
            remainImgObj.SetActive(true);
            pourImgObj.SetActive(false);
            wineBack[0].SetActive(false);
            wineBack[1].SetActive(false);
            wpriceObj.SetActive(true);
            priceObj[0].SetActive(false);
            priceObj[1].SetActive(false);
            wnoticeObj.text = "원";
            wcontentObj.text = Global.GetPriceFormat(Global.setInfo[curWineIndex].tagInfo.prepay_amt - Global.setInfo[curWineIndex].tagInfo.use_amt);
            soldoutObj[0].SetActive(false);
            soldoutObj[1].SetActive(false);
        }
        else if (workscenetype == WorkSceneType.pour)
        {
            remainImgObj.SetActive(false);
            pourImgObj.SetActive(true);
            wineBack[0].SetActive(false);
            wineBack[1].SetActive(false);
            wpriceObj.SetActive(true);
            priceObj[0].SetActive(false);
            priceObj[1].SetActive(false);
            wnoticeObj.text = "ml";
            if (Global.setInfo[curWineIndex].sell_type == 0 && is_last[curWineIndex])
            {
                wcontentObj.text = Global.GetPriceFormat(Global.setInfo[curWineIndex].max_limit);
            }
            else
            {
                wcontentObj.text = Global.GetPriceFormat(Global.setInfo[curWineIndex].wineInfo.quantity);
            }
            soldoutObj[0].SetActive(false);
            soldoutObj[1].SetActive(false);
        }
        else
        {
            //standby
            pourImgObj.SetActive(false);
            remainImgObj.SetActive(false);
            wpriceObj.SetActive(false);
            for(int i = 0; i < 2; i++)
            {
                soldoutObj[i].SetActive(false);
                wineBack[i].SetActive(true);
                priceObj[i].SetActive(true);
                if (Global.setInfo[i].sell_type == 0)
                {
                    noticeObj[i].text = "원/ml";
                    contentObj[i].text = Global.GetPriceFormat(Global.setInfo[i].wineInfo.ml_unit_price);
                }
                else
                {
                    noticeObj[i].text = "원/잔";
                    contentObj[i].text = Global.GetPriceFormat(Global.setInfo[i].wineInfo.cup_unit_price);
                }
            }
        }
        if (sceneSoldoutMode[0])
        {
            wineBack[0].SetActive(true);
            soldoutObj[0].SetActive(true);
        }
        if (sceneSoldoutMode[1])
        {
            wineBack[1].SetActive(true);
            soldoutObj[1].SetActive(true);
        }
    }

    void LoadInfo()
    {
        if (!is_self)
        {
            workscenetype = WorkSceneType.standby;
        }
        LoadWorkImg();
        switch (workscenetype)
        {
            case WorkSceneType.remain:
                {
                    //remain
                    soundObjs[3].Play();
                    break;
                }
        }
    }

    public void socketOpen(SocketIOEvent e)
    {
        string no = "{\"no\":\"" + Global.posInfo.appNo + "\"}";
        if(is_socket_open)
        {
            return;
        }
        is_socket_open = true;
        Debug.Log(no);
        socket.Emit("selftwoSetInfo", JSONObject.Create(no));
        Debug.Log("[SocketIO] Open received: " + e.name + " " + e.data);
    }

    public void LoadDeviceInfo(SocketIOEvent e)
    {
        JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
        Debug.Log("load deviceInfo = " + jsonNode);
        try
        {
            Global.setInfo[curWineIndex].board_channel = jsonNode["board_channel"].AsInt;
            Global.setInfo[curWineIndex].board_no = jsonNode["board_no"].AsInt;
            Global.setInfo[curWineIndex].tagGW_channel = jsonNode["taggw_channel"].AsInt;
            Global.setInfo[curWineIndex].tagGW_no = jsonNode["taggw_no"].AsInt;
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
    }

    public void ChangeWineInfo(SocketIOEvent e)
    {
        soundObjs[5].Play();
        JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
        int s_number = jsonNode["serial_number"].AsInt;
        int selected_index = -1;
        for (int i = 0; i < Global.setInfo.Length; i++)
        {
            if (s_number == Global.setInfo[i].serial_number)
            {
                selected_index = i;
                break;
            }
        }
        if (selected_index < 0)
        {
            return;
        }
        Global.setInfo[selected_index].wineInfo.server_id = jsonNode["product_id"].AsInt;
        if(selected_index != 0)
        {
            return;
        }
        string standby_url = Global.imgPath + "Standby" + Global.setInfo[0].wineInfo.server_id + ".jpg";
        string pous_url = Global.imgPath + "Pour" + Global.setInfo[0].wineInfo.server_id + ".jpg";
        if (File.Exists(standby_url))
        {
            File.Delete(standby_url);
        }
        if (File.Exists(pous_url))
        {
            File.Delete(pous_url);
        }
        DownloadWorkImg(false);
    }

    public void ChangeSetInfo(SocketIOEvent e)
    {
        JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
        Debug.Log(jsonNode);
        int s_number = jsonNode["serial_number"].AsInt;
        int selected_index = -1;
        for (int i = 0; i < Global.setInfo.Length; i++)
        {
            if (s_number == Global.setInfo[i].serial_number)
            {
                selected_index = i;
                break;
            }
        }
        if (selected_index < 0)
        {
            return;
        }

        Global.setInfo[selected_index].max_limit = jsonNode["max"].AsInt;
        Global.setInfo[selected_index].sensor_spec = jsonNode["sensor"].AsInt;
        Global.setInfo[selected_index].open_time = jsonNode["opentime"].AsInt;
        Global.setInfo[selected_index].sell_type = jsonNode["sell_type"].AsInt;
        Global.setInfo[selected_index].decarbo_time = jsonNode["decarbonation"].AsInt;
        Global.setInfo[selected_index].board_no = jsonNode["board_no"].AsInt;
        Global.setInfo[selected_index].board_channel = jsonNode["board_channel"].AsInt;
        Global.setInfo[selected_index].tagGW_no = jsonNode["gw_no"].AsInt;
        Global.setInfo[selected_index].tagGW_channel = jsonNode["gw_channel"].AsInt;
        is_loaded[selected_index] = false;
        if (Global.setInfo[selected_index].board_no != 0 && Global.setInfo[selected_index].board_channel != 0 && Global.setInfo[selected_index].tagGW_no != 0 && Global.setInfo[selected_index].tagGW_channel != 0)
        {
            is_loaded[selected_index] = true;
        }
    }

    DateTime open_market_time = new DateTime();
    DateTime open_market_timeA = new DateTime();
    public void OpenShopEventHandler(SocketIOEvent e)
    {
        soundObjs[6].Play();
        open_market_time = DateTime.Now;
        open_market_timeA = open_market_time.AddMinutes(Global.setInfo[curWineIndex].open_time);
        shop_open_popup.SetActive(true);
    }

    public void CloseShopEventHandler(SocketIOEvent e)
    {
        soundObjs[7].Play();
        JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
        int res = jsonNode["res"].AsInt;
        if (res == 1)
        {
            decarbonate_popup.SetActive(true);
            StartCoroutine(shopDecarbonate());
        }
        else
        {
            onShowScene(SceneStep.work);
            workscenetype = WorkSceneType.standby;
            LoadInfo();
        }
    }

    IEnumerator shopDecarbonate()
    {
        Debug.Log("stop decarbonate from shop close event.");
        yield return new WaitForSeconds(Global.setInfo[curWineIndex].decarbo_time);
        if (!shopCloseHandType && socket != null)
        {
            string data1 = "{\"board_no\":\"" + Global.setInfo[curWineIndex].board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo[curWineIndex].board_channel + "\"," +
                "\"valve\":\"" + 1 + "\"," +
                "\"status\":\"" + 0 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data1));
            decarbonate_popup.SetActive(false);
        }
        onShowScene(SceneStep.work);
        workscenetype = WorkSceneType.standby;
        LoadInfo();
    }

    public void RepairingDevice(SocketIOEvent e)
    {
        devicecheckingPopup.SetActive(true);
    }

    public void SoldoutEventHandler(SocketIOEvent e)
    {
        try
        {
            Debug.Log("[SocketIO] Soldout received: " + e.name + " " + e.data);
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            int is_soldout = jsonNode["is_soldout"].AsInt;
            if (is_soldout == 1)
            {
                soundObjs[1].Play();
                onShowScene(SceneStep.work);
                Global.setInfo[curWineIndex].wineInfo.is_soldout = true;
                workscenetype = WorkSceneType.standby;
                LoadInfo();
            }
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    DateTime open_tagResponse_time = new DateTime();
    DateTime open_tagResponse_timeA = new DateTime();

    public void FlowmeterValueEventHandler(SocketIOEvent e)
    {
        try
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            Debug.Log(jsonNode);
            long flowmeter_value = jsonNode["flowmeter_value"].AsLong;
            int serial_number = jsonNode["serial_number"].AsInt;
            int app_type = jsonNode["app_type"].AsInt;
            int appNo = jsonNode["appNo"].AsInt;
            int board_no = jsonNode["board_no"].AsInt;
            int ch_value = jsonNode["ch_value"].AsInt;
            standBTFlag = true;
            curWineIndex = -1;
            for (int i = 0; i < Global.setInfo.Length; i++)
            {
                if (Global.setInfo[i].serial_number == serial_number)
                {
                    curWineIndex = i;
                    break;
                }
            }
            if (curWineIndex == -1)
            {
                curWineIndex = 0;
                return;
            }

            workscenetype = WorkSceneType.pour;
            LoadInfo();
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    public void FlowmeterFinishEventHandler(SocketIOEvent e)
    {
        JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
        Debug.Log(jsonNode);
        long flowmeter_value = jsonNode["flowmeter_value"].AsLong;
        int serial_number = jsonNode["serial_number"].AsInt;
        int status = jsonNode["status"].AsInt;
        string tag_data = jsonNode["tag_data"];

        curWineIndex = -1;
        for(int i = 0 ; i < Global.setInfo.Length ; i++) {
            if(Global.setInfo[i].serial_number == serial_number) {
                curWineIndex = i;
                break;
            }
        }
        if(curWineIndex == -1) {
            curWineIndex = 0;
            return;
        }

        int temp;
        switch (status)
        {
            case 0:
                {
                    //정상종료
                    if (Global.setInfo[curWineIndex].tagInfo.is_pay_after == 1)
                    {
                        onShowScene(SceneStep.work);
                        workscenetype = WorkSceneType.standby;
                        soundObjs[4].Play();
                        temp = curWineIndex;
                        for (int i = 0; i < 2; i++)
                        {
                            curWineIndex = i;
                            LoadInfo();
                        }
                        curWineIndex = temp;
                    }
                    else
                    {
                        workscenetype = WorkSceneType.remain;
                        StartCoroutine(ReturntoStandby());
                    }
                    break;
                };
            case 1:
                {
                    //MAX차단
                    //soundObjs[2].Play();
                    workscenetype = WorkSceneType.pour;
                    StartCoroutine(ReturntoStandby());
                    break;
                };
            case 2:
                {
                    //EMPTY로 차단
                    workscenetype = WorkSceneType.pour;
                    StartCoroutine(ReturntoStandby());
                    break;
                };
            case 3:
                {
                    //SOLDOUT로 차단
                    sceneSoldoutMode[curWineIndex] = true;
                    soundObjs[1].Play();
                    temp = curWineIndex;
                    for (int i = 0; i < 2; i++)
                    {
                        curWineIndex = i;
                        LoadInfo();
                    }
                    curWineIndex = temp;
                    break;
                };
        }
        is_last[curWineIndex] = true;
    }

    public void socketError(SocketIOEvent e)
    {
        Debug.Log("[SocketIO] Error received: " + e.name + " " + e.data);
    }

    public void socketClose(SocketIOEvent e)
    {
        is_socket_open = false;
        Debug.Log("[SocketIO] Close received: " + e.name + " " + e.data);
    }

    IEnumerator ReturntoStandby()
    {
        yield return new WaitForSeconds(3f);
        onShowScene(SceneStep.work);
        workscenetype = WorkSceneType.standby;
        soundObjs[4].Play();
        LoadInfo();
    }

    public void onConfirmDeviceCheckingPopup()
    {
        devicecheckingPopup.SetActive(false);
        string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo[curWineIndex].tagGW_no + "\"," +
            "\"ch_value\":\"" + Global.setInfo[curWineIndex].tagGW_channel + "\"," +
            "\"status\":\"" + 1 + "\"}";
        socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));
    }

    public void OpenSettingTimeOver()
    {
        string tagGWData1 = "{\"tagGW_no\":\"" + Global.setInfo[curWineIndex].tagGW_no + "\"," +
            "\"ch_value\":\"" +100 + "\"," +
            "\"status\":\"" + 1 + "\"}";
        socket.Emit("deviceTagLock", JSONObject.Create(tagGWData1));

        string data = "{\"board_no\":\"" + Global.setInfo[curWineIndex].board_no + "\"," +
            "\"ch_value\":\"" + 100 + "\"," +
            "\"valve\":\"" + 0 + "\"," +
            "\"status\":\"" + 0 + "\"}";
        socket.Emit("boardValveCtrl", JSONObject.Create(data));
        open_market_time = new DateTime();
        shopFlag = true;
        standBTFlag = true;
        shop_open_popup.SetActive(false);
    }

    public void onConfirmShopOpenPopup()
    {
        shop_open_popup.SetActive(false);
        onShowScene(SceneStep.work);
        workscenetype = WorkSceneType.standby;
        LoadInfo();
        shopFlag = true;
        OpenSettingTimeOver();
    }

    public void onConfirmDecarbonatePopup()
    {
        Debug.Log("confirm decarbonation");
        decarbonate_popup.SetActive(false);
        workscenetype = WorkSceneType.standby;
        onShowScene(SceneStep.work);
        LoadInfo();
        if (socket != null)
        {
            string data = "{\"tagGW_no\":\"" + Global.setInfo[curWineIndex].tagGW_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo[curWineIndex].tagGW_channel + "\"," +
                "\"status\":\"" + 1 + "\"}";
            socket.Emit("deviceTagLock", JSONObject.Create(data));

            data = "{\"board_no\":\"" + Global.setInfo[curWineIndex].board_no + "\"," +
                "\"ch_value\":\"" + Global.setInfo[curWineIndex].tagGW_channel + "\"," +
                "\"valve\":\"" + 1 + "\"," +
                "\"status\":\"" + 0 + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));
        }
        shopCloseHandType = true;
    }

    //download image
    IEnumerator downloadImage(string url, string pathToSaveImage, GameObject imgObj)
    {
        yield return new WaitForSeconds(0.001f);
        Image img = imgObj.GetComponent<Image>();
        if (File.Exists(pathToSaveImage))
        {
            Debug.Log(pathToSaveImage + " exists");
            StartCoroutine(LoadPictureToTexture(pathToSaveImage, img));
        }
        else
        {
            Debug.Log(pathToSaveImage + " downloading--");
            WWW www = new WWW(url);
            StartCoroutine(_downloadImage(www, pathToSaveImage, img));
        }
    }

    IEnumerator LoadPictureToTexture(string name, Image img)
    {
        //Debug.Log("load image = " + Global.prePath + name);
        WWW pictureWWW = new WWW(Global.prePath + name);
        yield return pictureWWW;
        try
        {
            if (img != null)
            {
                img.sprite = Sprite.Create(pictureWWW.texture, new Rect(0, 0, pictureWWW.texture.width, pictureWWW.texture.height), new Vector2(0, 0), 8f, 0, SpriteMeshType.FullRect);
            }
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
    }

    private IEnumerator _downloadImage(WWW www, string savePath, Image img)
    {
        yield return www;
        //Check if we failed to send
        if (string.IsNullOrEmpty(www.error))
        {
            saveImage(savePath, www.bytes, img);
        }
        else
        {
            UnityEngine.Debug.Log("Error: " + www.error);
        }
    }

    void saveImage(string path, byte[] imageBytes, Image img)
    {
        try
        {
            //Create Directory if it does not exist
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            File.WriteAllBytes(path, imageBytes);
            //Debug.Log("Download Image: " + path.Replace("/", "\\"));
            StartCoroutine(LoadPictureToTexture(path, img));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed To Save Data to: " + path.Replace("/", "\\"));
            Debug.LogWarning("Error: " + e.Message);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (open_market_time == new DateTime())
        {
            return;
        }
        if (DateTime.Now >= open_market_timeA && shopFlag == false)
        {
            OpenSettingTimeOver();
        }
        if (open_tagResponse_time == new DateTime())
        {
            return;
        }
        if (DateTime.Now >= open_tagResponse_timeA && standBTFlag == false)
        {
            workscenetype = WorkSceneType.standby;
            onShowScene(SceneStep.work);
            LoadInfo();
        }
    }

    int order = 0;
    public void onClickOrder1()
    {
        Debug.Log("Clicklefttop");
        if (Global.setInfo[curWineIndex].wineInfo.is_soldout == true)
        {
            WWWForm form = new WWWForm();
            form.AddField("serial_number", Global.setInfo[curWineIndex].serial_number);
            WWW www = new WWW(Global.api_url + Global.cancel_soldout_api, form);
            StartCoroutine(CancelSoldout(www));
        }
        order = 1;
    }

    public void onClickOrder2()
    {
        if (order == 1)
        {
            order = 2;
        }
        else
        {
            order = 0;
        }
    }

    public void onClickOrder3()
    {
        if (order == 2)
        {
            order = 3;
        }
        else
        {
            order = 0;
        }
    }

    public void onClickOrder4()
    {
        if (order == 3)
        {
            onShowScene(SceneStep.setting);
            order = 0;
        }
        else
        {
            order = 0;
        }
    }

    IEnumerator CancelSoldout(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            int result = jsonNode["suc"].AsInt;
            if (result == 1)
            {
                workscenetype = WorkSceneType.standby;
                onShowScene(SceneStep.work);
                LoadInfo();
                if (socket != null)
                {
                    string tagGWData = "{\"tagGW_no\":\"" + Global.setInfo[curWineIndex].tagGW_no + "\"," +
                        "\"ch_value\":\"" + Global.setInfo[curWineIndex].tagGW_channel + "\"," +
                        "\"status\":\"" + 1 + "\"}";
                    socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));
                }
                err_popup.SetActive(false);
            }
            else
            {
                set_errStr.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
                set_errPopup.SetActive(true);
            }
        }
        else
        {
            set_errStr.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
            set_errPopup.SetActive(true);
        }

    }

    float time = 0f;
    private bool is_socket_open = false;

    void FixedUpdate()
    {
        if (!Input.anyKey)
        {
            time += Time.deltaTime;
        }
        else
        {
            if (time != 0f)
            {
                soundObjs[0].Play();
                time = 0f;
            }
        }
    }
}
