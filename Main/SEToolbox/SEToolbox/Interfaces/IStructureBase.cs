﻿namespace SEToolbox.Interfaces
{
    using Sandbox.Common.ObjectBuilders;
    using SEToolbox.Interop;

    public interface IStructureBase
    {
        MyObjectBuilder_EntityBase EntityBase { get; set; }

        long EntityId { get; set; }

        MyPositionAndOrientation? PositionAndOrientation { get; set; }

        ClassType ClassType { get; set; }

        string DisplayName { get; set; }

        string Description { get; set; }

        double PlayerDistance { get; set; }

        string SerializedEntity { get; set; }

        void UpdateGeneralFromEntityBase();

        bool IsBusy { get; set; }

        void InitializeAsync();
    }
}