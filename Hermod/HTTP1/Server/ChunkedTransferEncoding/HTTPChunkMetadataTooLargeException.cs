/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 */

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Raised when chunked transfer-coding metadata exceeds a configured limit.
    /// </summary>
    public sealed class HTTPChunkMetadataTooLargeException(String  Component,
                                                           UInt64  Actual,
                                                           UInt64  Maximum)

        : IOException($"The HTTP chunk {Component} exceeds its configured limit of {Maximum} bytes/items.")

    {

        public String Component { get; } = Component;
        public UInt64 Actual    { get; } = Actual;
        public UInt64 Maximum   { get; } = Maximum;

    }

}
