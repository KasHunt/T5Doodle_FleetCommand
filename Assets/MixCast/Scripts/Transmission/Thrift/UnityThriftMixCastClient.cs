/**********************************************************************************
* Blueprint Reality Inc. CONFIDENTIAL
* 2023 Blueprint Reality Inc.
* All Rights Reserved.
*
* NOTICE:  All information contained herein is, and remains, the property of
* Blueprint Reality Inc. and its suppliers, if any.  The intellectual and
* technical concepts contained herein are proprietary to Blueprint Reality Inc.
* and its suppliers and may be covered by Patents, pending patents, and are
* protected by trade secret or copyright law.
*
* Dissemination of this information or reproduction of this material is strictly
* forbidden unless prior written permission is obtained from Blueprint Reality Inc.
***********************************************************************************/

#if UNITY_STANDALONE_WIN
using Thrift.Unity;
using Thrift.Proxy;
using Thrift.Configuration;

namespace BlueprintReality.MixCast.Thrift
{
    public class UnityThriftMixCastClient : UnityThriftClient
    {
        public new static T Get<T>() where T : ClientProxy
        {
            try
            {
                return UnityThriftClient.Get<T>();
            }
            catch
            {
                UnityEngine.Debug.LogError("Application is about to quit, fail to obtain the Thrift Client");
                return null;
            }
        }

        public new static T Get<T>(string address) where T : ClientProxy
        {
            try
            {
                return UnityThriftClient.Get<T>(address);
            }
            catch
            {
                UnityEngine.Debug.LogError("Application is about to quit, fail to obtain the Thrift Client, this is likely because of the missing thrift config");
                return null;
            }
        }

        public static T Get<T>(ConnectionConfig config) where T : ClientProxy
        {
            try
            {
                return UnityThriftClient.Get<T>(config.Address, config.ServerPriority, config.ServerType, config.TransportType, config.ProtocolType, config.ServerTimeout, config.ClientTimeout);
            }
            catch
            {
                UnityEngine.Debug.LogError("Application is about to quit, fail to obtain the Thrift Client, this is likely because of the missing thrift config");
                return null;
            }
        }

        public new static bool ValidateOnce<T>(bool nolog = false) where T : ClientProxy
        {
            try
            {
                return UnityThriftClient.ValidateOnce<T>(nolog);
            }
            catch
            {
                UnityEngine.Debug.LogError("Fail to validate the Thrift Client, this is likely because of the missing thrift config");
                return false;
            }
        }

        public new static bool Validate<T>(bool nolog = false) where T : ClientProxy
        {
            try
            {
                return UnityThriftClient.Validate<T>(nolog);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("Fail to validate the Thrift Client, this is likely because of the missing thrift config: " + ex);
                return false;
            }
        }

        public new static bool ValidateCentralHub()
        {
            return UnityThriftClient.ValidateCentralHub();
        }

        public new static bool ValidateFunction<T>(string function_name, bool createDefaultClientIfNecessary = true) where T : ClientProxy
        {
            try
            {
                return UnityThriftClient.ValidateFunction<T>(function_name, createDefaultClientIfNecessary);
            }
            catch
            {
                UnityEngine.Debug.LogError("Fail to validate the Thrift Function, this is likely because of the missing thrift config");
                return false;
            }
        }
    }
}
#endif
