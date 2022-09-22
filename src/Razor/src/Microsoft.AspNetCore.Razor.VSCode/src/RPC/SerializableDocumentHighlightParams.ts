/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import { SerializablePosition } from './SerializablePosition';
import { SerializableTextDocumentIdentifier } from './SerializableTextDocumentIdentifier';

export interface SerializableDocumentHighlightParams {
    hostDocument: SerializableTextDocumentIdentifier;
    projectedPosition: SerializablePosition;
}
