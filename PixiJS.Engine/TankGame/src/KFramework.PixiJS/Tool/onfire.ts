/**
 * 重新编写一个,必须绑定target. 原来的框架，移除的时候会有bug。
 * Created by xubing on 2022/12/29
 */

export interface Listener {
  cb: Function;
  target:any;
  once: boolean;
}

export interface EventsType {
  [eventName: string]: Listener[];
}

/**
 * const ee = new OnFire();
 *
 * ee.on('click', this.testfun,this);
 *
 * ee.emit('click', 1, 2, 3);
 * ee.fire('mouseover', {}); // same with emit
 *
 * ee.off('click', this,this.testfun);
 */
export default class OnFire {

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
