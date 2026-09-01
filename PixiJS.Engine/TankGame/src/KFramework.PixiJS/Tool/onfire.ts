export interface Listener 
{
  cb: Function;
  target:any;
  once: boolean;
}

export interface EventsType 
{
  [eventName: string]: Listener[];
}

export default class OnFire 
{

  static ver = '__VERSION__';

  // 所有事件的监听器
  es: EventsType = {};

  on(eventName: string, cb: Function,target:any, once: boolean = false) {
    if (!this.es[eventName]) {
      this.es[eventName] = [];
    }

    this.es[eventName].push({
      cb,
      target,
      once,
    });
  }

  once(eventName: string, cb: Function,target:any) {
    this.on(eventName, cb,target, true);
  }

  fire(eventName: string, ...params: any[]) {
    const listeners = this.es[eventName] || [];

    let l = listeners.length;

    for (let i = 0; i < l; i ++) {
      const { cb,target, once } = listeners[i];
      cb.apply(target, params);

      if (once) {
        listeners.splice(i, 1);
        i --;
        l --;
      }
    }
  }

  off(eventName?: string, target?:any,cb?: Function,) {
    // clean all
    if (eventName === undefined) {
      this.es = {};
    } else {
      if (cb === undefined) {
        // clean the eventName's listeners
        delete this.es[eventName];
      } else {
        const listeners = this.es[eventName] || [];
        // clean the event and listener
        let l = listeners.length;
        for (let i = 0; i < l; i ++) {
          if (listeners[i].target==target && listeners[i].cb === cb) {
            listeners.splice(i, 1);
            i --;
            l --;
          }
        }
      }
    }
  }

  // cname of fire
  emit(eventName: string, ...params: any[]) {
    this.fire(eventName, ...params);
  }
}
