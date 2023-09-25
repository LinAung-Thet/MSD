using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Nutek.CommonData;
using Nutek.Adlink;

namespace Nutek.IO
{
    public class CNutekIO
    {
        private const string S_CLASS_NAME = "Class Name: CNutekIO; ";
        private const string S_METHOD_NAME = "Method: ";

        public struct StIOSetupProperty
        {
            public List<short> lstInMolStartIndex;
            public List<short> lstOutMolStartIndex;
            public int iTotalIOInModule;
            public int iTotalIOInChannel;
        }

        public List<StIOPointDef> stIOPoint = new List<StIOPointDef>();
        private CAdlinkIO clsIO = null;

        private Thread tdReadIOStatus = null;
        private bool bStartReadIOStatus = false;

        private short shCardNo = 0;
        private short shConnIndex = 0;
        private short shTotalChannel = 0;
        private short shTotalIOPerChannel = 0;

        public CNutekIO(short shCardID, short shIndexIO)
        {
            shCardNo = shCardID;
            shConnIndex = shIndexIO;
        }

        public void GetIOPointConfigurationFromDB(List<StIOPointDef> stIOCnfg)
        {
            stIOPoint = stIOCnfg;
        }

        public CNutekErrorStatus InitializeHardwareCard(StIOSetupProperty stIOSetupSetting)
        {
            CNutekErrorStatus clsErrStatus = new CNutekErrorStatus();
            short shTotalChannelPerModule = (short)(stIOSetupSetting.iTotalIOInModule / stIOSetupSetting.iTotalIOInChannel);
            shTotalChannel = (short)((stIOSetupSetting.lstInMolStartIndex.Count + stIOSetupSetting.lstOutMolStartIndex.Count) * shTotalChannelPerModule);
            shTotalIOPerChannel = (short)stIOSetupSetting.iTotalIOInChannel;

            clsErrStatus = CAdlinkCardInitialize.InitializeAdlink(shCardNo);
            if (clsErrStatus.GetLastError().shID != CNutekIOERRCode.I_STATUS_OK)
            {
                ConvertErrorCodeToErrorStatusClass(CNutekIOERRCode.ERR_INIT_HARDWARE_CARD, string.Empty, "InitializeHardwareCard", ref clsErrStatus);
                return clsErrStatus;
            }

            clsErrStatus = CAdlinkCardInitialize.StartHSLCardIOConnectIndex(shCardNo, shConnIndex, shTotalChannel);
            if (clsErrStatus.GetLastError().shID != CNutekIOERRCode.I_STATUS_OK)
            {
                ConvertErrorCodeToErrorStatusClass(CNutekIOERRCode.ERR_START_HARDWARE_SERVICE, string.Empty, "InitializeHardwareCard", ref clsErrStatus);
                return clsErrStatus;
            }

            clsIO = new CAdlinkIO(shCardNo, shConnIndex);
            clsIO.SetupIO(stIOSetupSetting.lstInMolStartIndex, stIOSetupSetting.lstOutMolStartIndex,
                          stIOSetupSetting.iTotalIOInModule, stIOSetupSetting.iTotalIOInChannel);

            return clsErrStatus;
        }
        public CNutekErrorStatus DeactivateHardwareCard()
        {
            CNutekErrorStatus clsErrStatus = new CNutekErrorStatus();
            clsErrStatus = CAdlinkCardInitialize.StopHSLCardIOConnectIndex(shCardNo, shConnIndex);
            if (clsErrStatus.GetLastError().shID != CNutekIOERRCode.I_STATUS_OK)
            {
                ConvertErrorCodeToErrorStatusClass(CNutekIOERRCode.ERR_INIT_HARDWARE_CARD, string.Empty, "DeactivateHardwareCard", ref clsErrStatus);
                return clsErrStatus;
            }

            return clsErrStatus;
        }
        public bool IsIOCardOnline(short shModuleNo)
        {
            return CAdlinkCardInitialize.IsIOCardOnline(shCardNo, shConnIndex, shModuleNo);
        }

        public void StartReadMtnIOStatus()
        {
            bStartReadIOStatus = true;
            tdReadIOStatus = new Thread(new ThreadStart(ReadIOPointStatus));
            tdReadIOStatus.Start();
        }
        public void StopReadMtnIOStatus()
        {
            bStartReadIOStatus = false;
        }
        private void ReadIOPointStatus()
        {
            while (bStartReadIOStatus)
            {
                Thread.Sleep(10);
                clsIO.ReadIOPointStatus();
                SetIOPointBitStatus();
            }
        }
        private void SetIOPointBitStatus()
        {
            // IO Status
            short i = 0, shChannelNo = 0, shIONo;		//i is up to IO_MAX=128
            StIOPointDef stTempIOPoint = new StIOPointDef();

            for (shChannelNo = 0; shChannelNo < shTotalChannel; shChannelNo++)
            {
                for (shIONo = 0; shIONo < shTotalIOPerChannel; shIONo++)
                {
                    stTempIOPoint = new StIOPointDef();
                    stTempIOPoint = stIOPoint[i];
                    stTempIOPoint.bStatus = clsIO.GetChannelBitStatus()[shChannelNo, shIONo];
                    stIOPoint[i] = stTempIOPoint;
                    i++;
                }
                shIONo = 0; //Reset
            }
        }

        public bool GetIOPointStatus(short shIO_ID)
        {
            if (!stIOPoint[shIO_ID].bNormallyOpen) //Normally Close
                return (!stIOPoint[shIO_ID].bStatus);

            return stIOPoint[shIO_ID].bStatus;	//Normally Open
        }
        public CNutekErrorStatus WriteIOPointOutput(short shIO_ID, bool bOn)
        {
            short i = 0;
            CNutekErrorStatus clsErrStatus = new CNutekErrorStatus();

            for (i = 0; i < stIOPoint.Count; i++)
            {
                if (stIOPoint[i].shIO_ID == shIO_ID)
                {
                    if (!stIOPoint[i].bNormallyOpen)
                    {
                        bOn = !bOn;
                    }

                    clsErrStatus = clsIO.WriteIOPointStatus(stIOPoint[i].shModNo, stIOPoint[i].shChnlNo, bOn);
                    if (clsErrStatus.GetLastError().shID != CNutekIOERRCode.I_STATUS_OK)
                    {
                        ConvertErrorCodeToErrorStatusClass(CNutekIOERRCode.ERR_INIT_HARDWARE_CARD, string.Empty, "WriteIOPointOutput", ref clsErrStatus);
                        return clsErrStatus;
                    }
                    break;
                }
            }
            return clsErrStatus;
        }

        private void ConvertErrorCodeToErrorStatusClass(short shErrorCode, string sException, string sMethodName, ref CNutekErrorStatus clsErr)
        {
            CNutekErrorStatus.STErrorStatus stErrDetails = new CNutekErrorStatus.STErrorStatus();
            stErrDetails.shID = shErrorCode;
            stErrDetails.sDescription = GetErrorCodeDescription(shErrorCode);
            stErrDetails.sTracingMessage = S_CLASS_NAME + S_METHOD_NAME + sMethodName;
            stErrDetails.sExceptionMessage = sException;
            stErrDetails.sRemedy = GetErrorCodeRemedy(shErrorCode);
            stErrDetails.bIsStatus = false;
            clsErr.StackError(stErrDetails);
        }
        private string GetErrorCodeDescription(short shErrID)
        {
            switch (shErrID)
            {
                case CNutekIOERRCode.I_STATUS_OK:
                    return CNutekIOERRDescription.sStatus_OK;
                case CNutekIOERRCode.ERR_INIT_HARDWARE_CARD:
                    return CNutekIOERRDescription.sErr_Init_Hardware_Card;
                case CNutekIOERRCode.ERR_STOP_HARDWARE_SERVICE:
                    return CNutekIOERRDescription.sErr_Stop_Hardware_Service;
                case CNutekIOERRCode.ERR_START_HARDWARE_SERVICE:
                    return CNutekIOERRDescription.sErr_Start_Hardware_Service;
                case CNutekIOERRCode.ERR_MTN_AXS_GET_ERROR_COUNT:
                    return CNutekIOERRDescription.sErr_Mtn_Axs_Get_Error_Count;
                case CNutekIOERRCode.ERR_MTN_AXS_GET_IO_STATUS:
                    return CNutekIOERRDescription.sErr_Mtn_Axs_Get_IO_Status;
                case CNutekIOERRCode.ERR_MTN_AXS_GET_POS:
                    return CNutekIOERRDescription.sErr_Mtn_Axs_Get_Position;
                case CNutekIOERRCode.ERR_WRITE_IO_STATUS:
                    return CNutekIOERRDescription.sErr_Write_IO_Status;
                case CNutekIOERRCode.ERR_MTN_AXS_MOVE_ABS:
                    return CNutekIOERRDescription.sErr_Mtn_Axs_Move_Absolute;
                case CNutekIOERRCode.ERR_MTN_AXS_MOVE_REL:
                    return CNutekIOERRDescription.sErr_Mtn_Axs_Move_Relative;
                case CNutekIOERRCode.ERR_MTN_AXS_RESET_CNT_ERROR:
                    return CNutekIOERRDescription.sErr_Mtn_Axs_Reset_Counter_Error;
                case CNutekIOERRCode.ERR_MTN_AXS_SET_POS:
                    return CNutekIOERRDescription.sErr_Mtn_Axs_Set_Position;
                case CNutekIOERRCode.ERR_MTN_AXS_SET_SERVO_ONOFF:
                    return CNutekIOERRDescription.sErr_Mtn_Axs_Set_Servo_OnOff;
                case CNutekIOERRCode.ERR_MTN_AXS_STOP:
                    return CNutekIOERRDescription.sErr_Mtn_Axs_Stop;
                case CNutekIOERRCode.I_STATUS_NOK:
                default:
                    return CNutekIOERRDescription.sStatus_NOK;
            }
        }
        private string GetErrorCodeRemedy(short shErrID)
        {
            return string.Empty;
        }
    }

    class CNutekIOERRCode
    {
        public const short I_STATUS_NOK = -1;
        public const short I_STATUS_OK = 0;

        public const short ERR_INIT_HARDWARE_CARD = 1000;
        public const short ERR_START_HARDWARE_SERVICE = 1001;
        public const short ERR_STOP_HARDWARE_SERVICE = 1002;

        public const short ERR_WRITE_IO_STATUS = 2000;
        public const short ERR_MTN_AXS_GET_IO_STATUS = 2001;
        public const short ERR_MTN_AXS_GET_ERROR_COUNT = 2002;
        public const short ERR_MTN_AXS_RESET_CNT_ERROR = 2003;
        public const short ERR_MTN_AXS_MOVE_ABS = 2004;
        public const short ERR_MTN_AXS_MOVE_REL = 2005;
        public const short ERR_MTN_AXS_STOP = 2006;
        public const short ERR_MTN_AXS_SET_POS = 2007;
        public const short ERR_MTN_AXS_GET_POS = 2008;
        public const short ERR_MTN_AXS_SET_SERVO_ONOFF = 2009;
    }

    class CNutekIOERRDescription
    {
        public const string sStatus_NOK = "Status NOT OK, Reason Unknown";
        public const string sStatus_OK = "Status OK, No Error";

        public const string sErr_Init_Hardware_Card = "Initialize Hardware Card Error";
        public const string sErr_Start_Hardware_Service = "Start Hardware Service Error";
        public const string sErr_Stop_Hardware_Service = "Stop Hardware Service Error";

        public const string sErr_Write_IO_Status = "Motion Done Error";
        public const string sErr_Mtn_Axs_Get_IO_Status = "Get Motion IO Status Error";
        public const string sErr_Mtn_Axs_Get_Error_Count = "Get Motion Error Count Error";
        public const string sErr_Mtn_Axs_Reset_Counter_Error = "Error When Reset Counter Error";
        public const string sErr_Mtn_Axs_Move_Absolute = "Move Absolute Error";
        public const string sErr_Mtn_Axs_Move_Relative = "Move Relative Error";
        public const string sErr_Mtn_Axs_Stop = "Stop Motion Error";
        public const string sErr_Mtn_Axs_Set_Position = "Set Position Error";
        public const string sErr_Mtn_Axs_Get_Position = "Get Position Error";
        public const string sErr_Mtn_Axs_Set_Servo_OnOff = "Error while Set Servo to ON or OFF";
    }
}
