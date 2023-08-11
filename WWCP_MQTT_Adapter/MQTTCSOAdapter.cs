/*
 * Copyright (c) 2015-2023 GraphDefined GmbH
 * This file is part of WWCP MQTT <https://github.com/OpenChargingCloud/WWCP_MQTT>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using Newtonsoft.Json.Linq;

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Logging;

#endregion

namespace cloud.charging.open.protocols.MQTT
{

    /// <summary>
    /// Publish charging station operator information via MQTT.
    /// </summary>
    public class MQTTCSOAdapter : WWCP.AWWCPEMPAdapter<WWCP.ChargeDetailRecord>,
                                  WWCP.IEMPRoamingProvider,
                                  IEquatable <MQTTCSOAdapter>,
                                  IComparable<MQTTCSOAdapter>,
                                  IComparable
    {

        #region Data

        //protected readonly  SemaphoreSlim  DataAndStatusLock     = new(1, 1);

        //protected readonly  TimeSpan       MaxLockWaitingTime    = TimeSpan.FromSeconds(120);


        /// <summary>
        /// The default logging context.
        /// </summary>
        public  const       String         DefaultLoggingContext        = "MQTT_CSOAdapter";

        public  const       String         DefaultHTTPAPI_LoggingPath   = "default";

        public  const       String         DefaultHTTPAPI_LogfileName   = "MQTT_CSOAdapter.log";


        private readonly  MqttFactory      mqttFactory                  = new ();
        private           IMqttClient      mqttClient;

        #endregion

        #region Properties

        IId WWCP.IAuthorizeStartStop.AuthId
            => Id;

        IId WWCP.ISendChargeDetailRecords.SendChargeDetailRecordsId
            => Id;

        public String MQTTHostname    { get; }

        public IPPort MQTTTCPPort     { get; }

        #endregion

        #region Events

        // from MQTT CSO
        public event WWCP.OnAuthorizeStartRequestDelegate?       OnAuthorizeStartRequest;
        public event WWCP.OnAuthorizeStartResponseDelegate?      OnAuthorizeStartResponse;

        public event WWCP.OnAuthorizeStopRequestDelegate?        OnAuthorizeStopRequest;
        public event WWCP.OnAuthorizeStopResponseDelegate?       OnAuthorizeStopResponse;

        public event WWCP.OnNewChargingSessionDelegate?          OnNewChargingSession;

        public event WWCP.OnSendCDRsRequestDelegate?             OnChargeDetailRecordRequest;
        public event WWCP.OnSendCDRsResponseDelegate?            OnChargeDetailRecordResponse;
        public event WWCP.OnNewChargeDetailRecordDelegate?       OnNewChargeDetailRecord;



        // from MQTT EMSP
        public event WWCP.OnReserveRequestDelegate?              OnReserveRequest;
        public event WWCP.OnReserveResponseDelegate?             OnReserveResponse;
        public event WWCP.OnNewReservationDelegate?              OnNewReservation;

        public event WWCP.OnCancelReservationRequestDelegate?    OnCancelReservationRequest;
        public event WWCP.OnCancelReservationResponseDelegate?   OnCancelReservationResponse;
        public event WWCP.OnReservationCanceledDelegate?         OnReservationCanceled;

        public event WWCP.OnRemoteStartRequestDelegate?          OnRemoteStartRequest;
        public event WWCP.OnRemoteStartResponseDelegate?         OnRemoteStartResponse;

        public event WWCP.OnRemoteStopRequestDelegate?           OnRemoteStopRequest;
        public event WWCP.OnRemoteStopResponseDelegate?          OnRemoteStopResponse;

        public event WWCP.OnGetCDRsRequestDelegate?              OnGetChargeDetailRecordsRequest;
        public event WWCP.OnGetCDRsResponseDelegate?             OnGetChargeDetailRecordsResponse;
        public event WWCP.OnSendCDRsResponseDelegate?            OnSendCDRsResponse;

        #endregion


        #region Constructor(s)

        public MQTTCSOAdapter(WWCP.EMPRoamingProvider_Id                       Id,
                              I18NString                                       Name,
                              I18NString                                       Description,
                              WWCP.RoamingNetwork                              RoamingNetwork,

                              String                                           MQTTHostname,
                              IPPort?                                          MQTTTCPPort                         = null,

                              WWCP.IncludeChargingStationOperatorIdDelegate?   IncludeChargingStationOperatorIds   = null,
                              WWCP.IncludeChargingStationOperatorDelegate?     IncludeChargingStationOperators     = null,
                              WWCP.IncludeChargingPoolIdDelegate?              IncludeChargingPoolIds              = null,
                              WWCP.IncludeChargingPoolDelegate?                IncludeChargingPools                = null,
                              WWCP.IncludeChargingStationIdDelegate?           IncludeChargingStationIds           = null,
                              WWCP.IncludeChargingStationDelegate?             IncludeChargingStations             = null,
                              WWCP.IncludeEVSEIdDelegate?                      IncludeEVSEIds                      = null,
                              WWCP.IncludeEVSEDelegate?                        IncludeEVSEs                        = null,

                              WWCP.ChargeDetailRecordFilterDelegate?           ChargeDetailRecordFilter            = null,

                              Boolean                                          DisablePushData                     = false,
                              Boolean                                          DisablePushAdminStatus              = true,
                              Boolean                                          DisablePushStatus                   = false,
                              Boolean                                          DisablePushEnergyStatus             = false,
                              Boolean                                          DisableAuthentication               = false,
                              Boolean                                          DisableSendChargeDetailRecords      = false,

                              Boolean?                                         IsDevelopment                       = null,
                              IEnumerable<String>?                             DevelopmentServers                  = null,
                              Boolean?                                         DisableLogging                      = false,
                              String?                                          LoggingPath                         = DefaultHTTPAPI_LoggingPath,
                              String?                                          LoggingContext                      = DefaultLoggingContext,
                              String?                                          LogfileName                         = DefaultHTTPAPI_LogfileName,
                              LogfileCreatorDelegate?                          LogfileCreator                      = null,

                              String?                                          ClientsLoggingPath                  = DefaultHTTPAPI_LoggingPath,
                              String?                                          ClientsLoggingContext               = DefaultLoggingContext,
                              LogfileCreatorDelegate?                          ClientsLogfileCreator               = null,
                              DNSClient?                                       DNSClient                           = null)


            : base(Id,
                   RoamingNetwork,
                   Name,
                   Description,

                   IncludeEVSEIds,
                   IncludeEVSEs,
                   null,
                   null,
                   null,
                   null,
                   null,
                   null,
                   ChargeDetailRecordFilter,

                   null,
                   null,
                   null,
                   null,

                   DisablePushData,
                   DisablePushAdminStatus,
                   DisablePushStatus,
                   true,
                   DisablePushEnergyStatus,
                   DisableAuthentication,
                   DisableSendChargeDetailRecords,

                   String.Empty,
                   null,
                   null,

                   IsDevelopment,
                   DevelopmentServers,
                   DisableLogging,
                   LoggingPath,
                   LoggingContext,
                   LogfileName,
                   LogfileCreator,

                   ClientsLoggingPath,
                   ClientsLoggingContext,
                   ClientsLogfileCreator,
                   DNSClient)


        {

            this.MQTTHostname  = MQTTHostname;
            this.MQTTTCPPort   = MQTTTCPPort ?? IPPort.SSH;
            this.mqttClient    = mqttFactory.CreateMqttClient();  // IMqttNetLogger

        }

        #endregion


        #region Connect()

        public async Task<MqttClientConnectResult> Connect()
        {

            var result = await mqttClient.ConnectAsync(new MqttClientOptionsBuilder().
                                                           WithClientId       (Guid.NewGuid().ToString()).
                                                           WithTcpServer      (MQTTHostname,
                                                                               MQTTTCPPort.ToUInt16()).
                                                           WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500).
                                                           Build());

            return result;

        }

        #endregion

        #region Disconnect()

        public async Task Disconnect(MqttClientDisconnectOptionsReason  Reason                  = MqttClientDisconnectOptionsReason.NormalDisconnection,
                                     String?                            ReasonString            = null,
                                     UInt32                             SessionExpiryInterval   = 0,
                                     List<MqttUserProperty>?            UserProperties          = null,
                                     CancellationToken                  CancellationToken       = default)
        {

            await mqttClient.DisconnectAsync(Reason,
                                             ReasonString,
                                             SessionExpiryInterval,
                                             UserProperties,
                                             CancellationToken);

        }

        #endregion


        #region (Set/Add/Update/Delete) EVSE(s)...

        #region UpdateEVSEAdminStatus (EVSEAdminStatusUpdates,  TransmissionType = Enqueue, ...)

        /// <summary>
        /// Update the given enumeration of EVSE admin status updates.
        /// </summary>
        /// <param name="EVSEAdminStatusUpdates">An enumeration of EVSE admin status updates.</param>
        /// <param name="TransmissionType">Whether to send the EVSE admin status updates directly or enqueue it for a while.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public override async Task<WWCP.PushEVSEAdminStatusResult>

            UpdateEVSEAdminStatus(IEnumerable<WWCP.EVSEAdminStatusUpdate>  EVSEAdminStatusUpdates,
                                  WWCP.TransmissionTypes                   TransmissionType    = WWCP.TransmissionTypes.Enqueue,

                                  DateTime?                                Timestamp           = null,
                                  EventTracking_Id?                        EventTrackingId     = null,
                                  TimeSpan?                                RequestTimeout      = null,
                                  CancellationToken                        CancellationToken   = default)

        {

            #region Initial checks

            if (!EVSEAdminStatusUpdates.Any())
                return WWCP.PushEVSEAdminStatusResult.NoOperation(Id, this);

            WWCP.PushEVSEAdminStatusResult? result = null;

            #endregion


            var mqttPublishResult = await mqttClient.PublishAsync(new MqttApplicationMessageBuilder().
                                                                      WithTopic("EVSE/adminStatus/updates").
                                                                      WithPayload(new JArray(
                                                                                      EVSEAdminStatusUpdates.Select(statusUpdate => new JObject(
                                                                                                                                        new JProperty("evseId",    statusUpdate.Id.                 ToString()),
                                                                                                                                        new JProperty("timestamp", statusUpdate.NewStatus.Timestamp.ToIso8601()),
                                                                                                                                        new JProperty("status",    statusUpdate.NewStatus.Value.    ToString())
                                                                                                                                    ))
                                                                                  ).ToString(Newtonsoft.Json.Formatting.None)).
                                                                      WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce).
                                                                      WithRetainFlag(false).
                                                                      Build());



            result = mqttPublishResult.IsSuccess

                         ? WWCP.PushEVSEAdminStatusResult.Success(Id, this)

                         : WWCP.PushEVSEAdminStatusResult.Error(
                               AuthId:            Id,
                               ISendAdminStatus:  this,
                               RejectedEVSEs:     EVSEAdminStatusUpdates,
                               Description:       $"{mqttPublishResult.ReasonCode} {mqttPublishResult.ReasonString}",
                               Warnings:          null,
                               Runtime:           null
                           );


            return result;

        }

        #endregion

        #region UpdateEVSEStatus      (EVSEStatusUpdates,       TransmissionType = Enqueue, ...)

        /// <summary>
        /// Update the given enumeration of EVSE status updates.
        /// </summary>
        /// <param name="EVSEStatusUpdates">An enumeration of EVSE status updates.</param>
        /// <param name="TransmissionType">Whether to send the EVSE status updates directly or enqueue it for a while.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public override async Task<WWCP.PushEVSEStatusResult>

            UpdateEVSEStatus(IEnumerable<WWCP.EVSEStatusUpdate>  EVSEStatusUpdates,
                             WWCP.TransmissionTypes              TransmissionType    = WWCP.TransmissionTypes.Enqueue,

                             DateTime?                           Timestamp           = null,
                             EventTracking_Id?                   EventTrackingId     = null,
                             TimeSpan?                           RequestTimeout      = null,
                             CancellationToken                   CancellationToken   = default)

        {

            #region Initial checks

            if (!EVSEStatusUpdates.Any())
                return WWCP.PushEVSEStatusResult.NoOperation(Id, this);

            WWCP.PushEVSEStatusResult? result = null;

            #endregion


            var mqttPublishResult = await mqttClient.PublishAsync(new MqttApplicationMessageBuilder().
                                                                      WithTopic("EVSE/status/updates").
                                                                      WithPayload(new JArray(
                                                                                      EVSEStatusUpdates.Select(statusUpdate => new JObject(
                                                                                                                               new JProperty("evseId",    statusUpdate.Id.                 ToString()),
                                                                                                                               new JProperty("timestamp", statusUpdate.NewStatus.Timestamp.ToIso8601()),
                                                                                                                               new JProperty("status",    statusUpdate.NewStatus.Value.    ToString())
                                                                                                                           ))
                                                                                  ).ToString(Newtonsoft.Json.Formatting.None)).
                                                                      WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce).
                                                                      WithRetainFlag(false).
                                                                      Build());



            result = mqttPublishResult.IsSuccess

                         ? WWCP.PushEVSEStatusResult.Success(Id, this)

                         : WWCP.PushEVSEStatusResult.Error(
                               AuthId:         Id,
                               ISendStatus:    this,
                               RejectedEVSEs:  EVSEStatusUpdates,
                               Description:    $"{mqttPublishResult.ReasonCode} {mqttPublishResult.ReasonString}",
                               Warnings:       null,
                               Runtime:        null
                           );


            return result;

        }

        #endregion

        #region UpdateEVSEEnergyStatus(EVSEEnergyStatusUpdates, TransmissionType = Enqueue, ...)

        /// <summary>
        /// Update the given enumeration of EVSE status updates.
        /// </summary>
        /// <param name="EVSEEnergyStatusUpdates">An enumeration of EVSE status updates.</param>
        /// <param name="TransmissionType">Whether to send the EVSE status updates directly or enqueue it for a while.</param>
        /// 
        /// <param name="Timestamp">The optional timestamp of the request.</param>
        /// <param name="EventTrackingId">An optional event tracking identification for correlating this request with other events.</param>
        /// <param name="RequestTimeout">An optional timeout for this request.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public override async Task<WWCP.PushEVSEEnergyStatusResult>

            UpdateEVSEEnergyStatus(IEnumerable<WWCP.EVSEEnergyStatusUpdate>  EVSEEnergyStatusUpdates,
                                   WWCP.TransmissionTypes                    TransmissionType    = WWCP.TransmissionTypes.Enqueue,

                                   DateTime?                                 Timestamp           = null,
                                   EventTracking_Id?                         EventTrackingId     = null,
                                   TimeSpan?                                 RequestTimeout      = null,
                                   CancellationToken                         CancellationToken   = default)

        {

            #region Initial checks

            if (!EVSEEnergyStatusUpdates.Any())
                return WWCP.PushEVSEEnergyStatusResult.NoOperation(Id, this);

            WWCP.PushEVSEEnergyStatusResult? result = null;

            #endregion


            var mqttPublishResult = await mqttClient.PublishAsync(new MqttApplicationMessageBuilder().
                                                                      WithTopic("EVSE/energyStatus/updates").
                                                                      WithPayload(new JArray(
                                                                                      EVSEEnergyStatusUpdates.Select(statusUpdate => new JObject(
                                                                                                                                         new JProperty("evseId",     statusUpdate.Id.                     ToString()),
                                                                                                                                         new JProperty("timestamp",  statusUpdate.NewEnergyInfo.Timestamp.ToIso8601()),
                                                                                                                                         new JProperty("available",  statusUpdate.NewEnergyInfo.Value.Available),
                                                                                                                                         new JProperty("used",       statusUpdate.NewEnergyInfo.Value.Used)
                                                                                                                                     ))
                                                                                  ).ToString(Newtonsoft.Json.Formatting.None)).
                                                                      WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce).
                                                                      WithRetainFlag(false).
                                                                      Build());



            result = mqttPublishResult.IsSuccess

                         ? WWCP.PushEVSEEnergyStatusResult.Success(Id, this)

                         : WWCP.PushEVSEEnergyStatusResult.Error(
                               AuthId:             Id,
                               ISendEnergyStatus:  this,
                               RejectedEVSEs:      EVSEEnergyStatusUpdates,
                               Description:        $"{mqttPublishResult.ReasonCode} {mqttPublishResult.ReasonString}",
                               Warnings:           null,
                               Runtime:            null
                           );


            return result;

        }

        #endregion

        #endregion



        #region [remove me] AuthorizeStart/-Stop

        public Task<WWCP.AuthStartResult> AuthorizeStart(WWCP.LocalAuthentication LocalAuthentication, WWCP.ChargingLocation? ChargingLocation = null, WWCP.ChargingProduct? ChargingProduct = null, WWCP.ChargingSession_Id? SessionId = null, WWCP.ChargingSession_Id? CPOPartnerSessionId = null, WWCP.ChargingStationOperator_Id? OperatorId = null, DateTime? Timestamp = null, EventTracking_Id? EventTrackingId = null, TimeSpan? RequestTimeout = null, CancellationToken CancellationToken = default)
        {
            return Task.FromResult(WWCP.AuthStartResult.NotAuthorized(Id, this));
        }

        public Task<WWCP.AuthStopResult> AuthorizeStop(WWCP.ChargingSession_Id SessionId, WWCP.LocalAuthentication LocalAuthentication, WWCP.ChargingLocation? ChargingLocation = null, WWCP.ChargingSession_Id? CPOPartnerSessionId = null, WWCP.ChargingStationOperator_Id? OperatorId = null, DateTime? Timestamp = null, EventTracking_Id? EventTrackingId = null, TimeSpan? RequestTimeout = null, CancellationToken CancellationToken = default)
        {
            return Task.FromResult(WWCP.AuthStopResult.NotAuthorized(Id, this));
        }

        #endregion

        #region [remove me] SendChargeDetailRecords

        public Task<WWCP.SendCDRsResult> SendChargeDetailRecords(IEnumerable<WWCP.ChargeDetailRecord> ChargeDetailRecords, WWCP.TransmissionTypes TransmissionType = WWCP.TransmissionTypes.Enqueue, DateTime? Timestamp = null, EventTracking_Id EventTrackingId = null, TimeSpan? RequestTimeout = null, CancellationToken CancellationToken = default)
        {
            return Task.FromResult(WWCP.SendCDRsResult.NoOperation(org.GraphDefined.Vanaheimr.Illias.Timestamp.Now, Id, this, ChargeDetailRecords));
        }

        #endregion



        #region Skip/Flush

        protected override Boolean SkipFlushEVSEDataAndStatusQueues()
        {
            return true;
        }

        protected override Task FlushEVSEDataAndStatusQueues()
        {
            return Task.CompletedTask;
        }

        protected override Boolean SkipFlushEVSEFastStatusQueues()
        {
            return true;
        }

        protected override Task FlushEVSEFastStatusQueues()
        {
            return Task.CompletedTask;
        }

        protected override Boolean SkipFlushChargeDetailRecordsQueues()
        {
            return true;
        }

        protected override Task FlushChargeDetailRecordsQueues(IEnumerable<WWCP.ChargeDetailRecord> ChargeDetailsRecords)
        {
            return Task.CompletedTask;
        }

        #endregion


        #region Operator overloading

        #region Operator == (MQTTCSOAdapter1, MQTTCSOAdapter2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MQTTCSOAdapter1">A MQTT CSO adapter.</param>
        /// <param name="MQTTCSOAdapter2">Another MQTT CSO adapter.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (MQTTCSOAdapter MQTTCSOAdapter1,
                                           MQTTCSOAdapter MQTTCSOAdapter2)
        {

            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(MQTTCSOAdapter1, MQTTCSOAdapter2))
                return true;

            // If one is null, but not both, return false.
            if (MQTTCSOAdapter1 is null || MQTTCSOAdapter2 is null)
                return false;

            return MQTTCSOAdapter1.Equals(MQTTCSOAdapter2);

        }

        #endregion

        #region Operator != (MQTTCSOAdapter1, MQTTCSOAdapter2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MQTTCSOAdapter1">A MQTT CSO adapter.</param>
        /// <param name="MQTTCSOAdapter2">Another MQTT CSO adapter.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (MQTTCSOAdapter MQTTCSOAdapter1,
                                           MQTTCSOAdapter MQTTCSOAdapter2)

            => !(MQTTCSOAdapter1 == MQTTCSOAdapter2);

        #endregion

        #region Operator <  (MQTTCSOAdapter1, MQTTCSOAdapter2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MQTTCSOAdapter1">A MQTT CSO adapter.</param>
        /// <param name="MQTTCSOAdapter2">Another MQTT CSO adapter.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (MQTTCSOAdapter MQTTCSOAdapter1,
                                          MQTTCSOAdapter MQTTCSOAdapter2)
        {

            if (MQTTCSOAdapter1 is null)
                throw new ArgumentNullException(nameof(MQTTCSOAdapter1), "The given MQTT CSO adapter must not be null!");

            return MQTTCSOAdapter1.CompareTo(MQTTCSOAdapter2) < 0;

        }

        #endregion

        #region Operator <= (MQTTCSOAdapter1, MQTTCSOAdapter2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MQTTCSOAdapter1">A MQTT CSO adapter.</param>
        /// <param name="MQTTCSOAdapter2">Another MQTT CSO adapter.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (MQTTCSOAdapter MQTTCSOAdapter1,
                                           MQTTCSOAdapter MQTTCSOAdapter2)

            => !(MQTTCSOAdapter1 > MQTTCSOAdapter2);

        #endregion

        #region Operator >  (MQTTCSOAdapter1, MQTTCSOAdapter2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MQTTCSOAdapter1">A MQTT CSO adapter.</param>
        /// <param name="MQTTCSOAdapter2">Another MQTT CSO adapter.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (MQTTCSOAdapter MQTTCSOAdapter1,
                                          MQTTCSOAdapter MQTTCSOAdapter2)
        {

            if (MQTTCSOAdapter1 is null)
                throw new ArgumentNullException(nameof(MQTTCSOAdapter1), "The given MQTT CSO adapter must not be null!");

            return MQTTCSOAdapter1.CompareTo(MQTTCSOAdapter2) > 0;

        }

        #endregion

        #region Operator >= (MQTTCSOAdapter1, MQTTCSOAdapter2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="MQTTCSOAdapter1">A MQTT CSO adapter.</param>
        /// <param name="MQTTCSOAdapter2">Another MQTT CSO adapter.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (MQTTCSOAdapter MQTTCSOAdapter1,
                                           MQTTCSOAdapter MQTTCSOAdapter2)

            => !(MQTTCSOAdapter1 < MQTTCSOAdapter2);

        #endregion

        #endregion

        #region IComparable<MQTTCSOAdapter> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two MQTT CSO adapters.
        /// </summary>
        /// <param name="Object">A MQTT CSO adapter to compare with.</param>
        public override Int32 CompareTo(Object? Object)

            => Object is MQTTCSOAdapter mqttCSOAdapter
                   ? CompareTo(mqttCSOAdapter)
                   : throw new ArgumentException("The given object is not a MQTT CSO adapter!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(MQTTCSOAdapter)

        /// <summary>
        /// Compares two MQTT CSO adapters.
        /// </summary>
        /// <param name="MQTTCSOAdapter">A MQTT CSO adapter to compare with.</param>
        public Int32 CompareTo(MQTTCSOAdapter? MQTTCSOAdapter)
        {

            if (MQTTCSOAdapter is null)
                throw new ArgumentNullException(nameof(MQTTCSOAdapter),
                                                "The given MQTT CSO adapter must not be null!");

            return Id.CompareTo(MQTTCSOAdapter.Id);

        }

        #endregion

        #endregion

        #region IEquatable<MQTTCSOAdapter> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two MQTT CSO adapters for equality.
        /// </summary>
        /// <param name="Object">A MQTT CSO adapter to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is MQTTCSOAdapter ocpiCSOAdapter &&
                   Equals(ocpiCSOAdapter);

        #endregion

        #region Equals(MQTTCSOAdapter)

        /// <summary>
        /// Compares two MQTT CSO adapters for equality.
        /// </summary>
        /// <param name="MQTTCSOAdapter">A MQTT CSO adapter to compare with.</param>
        public Boolean Equals(MQTTCSOAdapter? MQTTCSOAdapter)

            => MQTTCSOAdapter is not null &&
                   Id.Equals(MQTTCSOAdapter.Id);

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override Int32 GetHashCode()

            => Id.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => Id.ToString();

        #endregion


    }

}
