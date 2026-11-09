import { getLastErrorMessage } from './bindings';
import { ResultCode } from './enums';

/** Thrown when the native library reports an error. result holds the ResultCode of the failure. */
export class Deskband11LibError extends Error {
    readonly result: ResultCode;

    constructor(result: ResultCode, message: string) {
        super(message);
        this.name = 'Deskband11LibError';
        this.result = result;
    }
}

export function checkResult(result: number): number {
    if (result < 0) throw new Deskband11LibError(result, getLastErrorMessage() || `The native call failed with result ${result}.`);
    return result;
}
