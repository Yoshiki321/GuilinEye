using UnityEngine;

namespace GuilinEye.Integration
{
    /// <summary>集成层装配：BP-01 投影台架、BP-02 总演出、BP-03 传感监督、BP-04 运维桥、BP-05 校准台。</summary>
    public class IntegrationBootstrap : MonoBehaviour
    {
        public bool buildProjectionRig = true;
        public bool buildShowDirector = true;
        public bool buildSensorSupervisor = true;
        public bool buildOpsBridge;
        public bool buildH5Feed = true;
        public bool buildCalibrationTool = true;

        ProjectionRig _rig;
        ShowDirector _director;
        SensorSupervisor _supervisor;
        OpsBridge _ops;
        H5FeedPoller _h5Feed;
        CalibrationTool _calibration;
        bool _built;

        public ProjectionRig Rig { get { return _rig; } }
        public ShowDirector Director { get { return _director; } }
        public SensorSupervisor Supervisor { get { return _supervisor; } }
        public OpsBridge Ops { get { return _ops; } }
        public H5FeedPoller H5Feed { get { return _h5Feed; } }
        public CalibrationTool Calibration { get { return _calibration; } }

        public void Build()
        {
            if (_built) return;
            _built = true;
            if (buildProjectionRig)
            {
                _rig = GetComponent<ProjectionRig>();
                if (_rig == null) _rig = gameObject.AddComponent<ProjectionRig>();
                _rig.Build();
            }

            if (buildShowDirector)
            {
                _director = GetComponent<ShowDirector>();
                if (_director == null) _director = gameObject.AddComponent<ShowDirector>();
                _director.Build();
            }

            if (buildSensorSupervisor)
            {
                _supervisor = GetComponent<SensorSupervisor>();
                if (_supervisor == null) _supervisor = gameObject.AddComponent<SensorSupervisor>();
                _supervisor.Build();
            }

            if (buildCalibrationTool)
            {
                _calibration = GetComponent<CalibrationTool>();
                if (_calibration == null) _calibration = gameObject.AddComponent<CalibrationTool>();
                _calibration.rig = _rig;
                _calibration.Build();
            }

            if (buildH5Feed)
            {
                _h5Feed = GetComponent<H5FeedPoller>();
                if (_h5Feed == null) _h5Feed = gameObject.AddComponent<H5FeedPoller>();
                _h5Feed.StartPoller();
            }

            if (buildOpsBridge)
            {
                _ops = GetComponent<OpsBridge>();
                if (_ops == null) _ops = gameObject.AddComponent<OpsBridge>();
                _ops.StartBridge();
            }

            Debug.Log("[Integration] BP-01—BP-05 + H5Feed 装配完成");
        }
    }
}
