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
using Thrift.Configuration;
using Thrift.Unity;
using Thrift.Proxy;

namespace BlueprintReality.MixCast.Thrift
{
    public class UnityThriftMixCastServer : UnityThriftServer
    {
        public new static T Get<T>() where T : HandlerProxy
        {
            try
            {
                return UnityThriftServer.Get<T>();
            }
            catch
            {
                UnityEngine.Debug.LogError("Application is about to quit, fail to obtain the Thrift Server, this is likely because of the missing thrift config");
                return null;
            }
        }

        public new static T Get<T>(ServerPriority priority) where T : HandlerProxy
        {
            try
            {
                return UnityThriftServer.Get<T>(priority);
            }
            catch
            {
                UnityEngine.Debug.LogError("Application is about to quit, fail to obtain the Thrift Server, this is likely because of the missing thrift config");
                return null;
            }
        }

        public static T Get<T>(ConnectionConfig config) where T : HandlerProxy
        {
            try
            {
                return UnityThriftServer.Get<T>(config.Address, config.ServerPriority, config.ServerType, config.TransportType, config.ProtocolType, config.ServerTimeout, config.ClientTimeout);
            }
            catch
            {
                UnityEngine.Debug.LogError("Application is about to quit, fail to obtain the Thrift Server, this is likely because of the missing thrift config");
                return null;
            }
        }
    }
}
#endif
