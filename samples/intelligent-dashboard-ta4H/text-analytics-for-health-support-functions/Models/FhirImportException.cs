// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;

namespace text_analytics_for_health_support_functions.Models
{
    public class FhirImportException : Exception
    {
        public FhirImportException(string message)
            : base(message)
        {
        }
    }
}
